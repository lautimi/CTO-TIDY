using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Koovra.Cto.AutocadAddin.Geometry;
using Koovra.Cto.AutocadAddin.Infrastructure;
using Koovra.Cto.AutocadAddin.Models;
using Koovra.Cto.AutocadAddin.Persistence;
using Koovra.Cto.Core;

namespace Koovra.Cto.AutocadAddin.Commands
{
    public class CalcularCtosCommand
    {
        [CommandMethod("CTO_CALCULAR", CommandFlags.Modal)]
        public void Execute()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db  = doc.Database;

            if (!SelectionContext.Instance.TryGetPostes(out var polesIds))
            {
                AcadLogger.Warn("No hay postes seleccionados. Ejecuta CTO_SELECCIONAR_POSTES primero.");
                return;
            }

            var stats = new CtoDistributor.Stats();

            using (doc.LockDocument())
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                CtoDistributor.DistributeAndStore(tr, db, polesIds, stats, AcadLogger.Info);
                tr.Commit();
            }

            AcadLogger.Info(
                $"[CTO_CALCULAR] Segmentos={stats.Segmentos}  " +
                $"CTOs totales={stats.TotalCtos}  HP>64={stats.OutOfRange}");

            if (stats.OutOfRange > 0)
                AcadLogger.Warn($"{stats.OutOfRange} segmento(s) con HP > 64; se usó el último rango de la tabla.");
        }
    }

    /// <summary>
    /// Lógica compartida entre CTO_CALCULAR y el paso 4 del panel.
    ///
    /// Unidad de agrupamiento: **SEGMENTO** (eje de calle). Justificación:
    ///   1 segmento = 2 frentes de manzana + 1 bloque CONT_HP.
    /// El HP está asociado al eje de calle y se reparte en UN SOLO frente (el PRIORIDAD).
    /// Agrupar por linga antes fue un bug: si un segmento tenía 2 lingas PRIORIDAD (una por
    /// frente), la distribución se corría dos veces con el mismo HP → cajas duplicadas.
    ///
    /// Para cada SEGMENTO:
    ///   1. Separar postes del segmento en PRIORIDAD vs resto (SEC/sin linga).
    ///   2. Postes no-PRIORIDAD → C_DESP=0, C_CREC=0 (no reciben cajas).
    ///   3. Si no hay PRIORIDAD, el segmento completo va en 0,0.
    ///   4. HP se toma del segmento (cualquier poste PRIORIDAD lo tiene).
    ///   5. LARGO_FRENTE se toma por voto mayoritario de los postes PRIORIDAD (warning si hay
    ///      inconsistencia).
    ///   6. Calcular C_DESP, C_CREC con la tabla oficial (HP × LARGO_FRENTE).
    ///   7. Ordenar postes PRIORIDAD por cercanía al midpoint del SEGMENTO (centrales primero).
    ///   8. Generar secuencia D,C,D,C,... y distribuir round-robin.
    ///   9. Escribir C_DESP/C_CREC en XData.
    /// </summary>
    internal static class CtoDistributor
    {
        public class Stats
        {
            public int Segmentos;
            public int TotalCtos;
            public int OutOfRange;
        }

        public static void DistributeAndStore(
            Transaction tr,
            Database    db,
            ObjectId[]  polesIds,
            Stats       stats,
            Action<string> logInfo = null)
        {
            // ── 1. Agrupar postes por ID_SEGMENT ──────────────────────────────
            var polesBySegmento = new Dictionary<string, List<ObjectId>>(
                StringComparer.OrdinalIgnoreCase);

            foreach (ObjectId poleId in polesIds)
            {
                string segId = XDataManager.GetString(tr, poleId, XDataKeys.ID_SEGMENT) ?? string.Empty;
                if (string.IsNullOrEmpty(segId))
                {
                    WriteZero(tr, poleId);
                    continue;
                }
                if (!polesBySegmento.TryGetValue(segId, out var list))
                    polesBySegmento[segId] = list = new List<ObjectId>();
                list.Add(poleId);
            }

            // ── 2. Procesar cada segmento ────────────────────────────────────
            foreach (var kv in polesBySegmento)
            {
                string segId    = kv.Key;
                var    polesAll = kv.Value;

                // Separar por tipo de linga: sólo PRIORIDAD recibe cajas.
                var priPoles   = new List<ObjectId>();
                var otherPoles = new List<ObjectId>();
                foreach (var pid in polesAll)
                {
                    string tipo = XDataManager.GetString(tr, pid, XDataKeys.LINGA_TIPO) ?? string.Empty;
                    if (tipo == XDataKeys.LINGA_PRIORIDAD) priPoles.Add(pid);
                    else                                   otherPoles.Add(pid);
                }

                // Postes no-PRIORIDAD → 0,0 siempre.
                foreach (var pid in otherPoles) WriteZero(tr, pid);

                if (priPoles.Count == 0) continue;  // Segmento sin PRIORIDAD: nada para desplegar.

                // HP del segmento (cualquier priPole lo tiene — es del eje de calle).
                int hp = XDataManager.GetInt(tr, priPoles[0], XDataKeys.HP) ?? 0;

                // LARGO_FRENTE por voto mayoritario (uniformidad esperada en priPoles).
                double largo = PickLargoFrenteByMajority(tr, priPoles, segId, logInfo);

                if (largo <= 0.0)
                {
                    foreach (var pid in priPoles) WriteZero(tr, pid);
                    logInfo?.Invoke(
                        $"  ⚠ Segmento {ShortHandle(segId)} sin LARGO_FRENTE resuelto — " +
                        $"{priPoles.Count} postes PRIORIDAD forzados a 0,0");
                    continue;
                }

                if (CtoCountCalculator.IsOutOfRange(hp)) stats.OutOfRange++;
                CtoCountCalculator.Result r = CtoCountCalculator.Calculate(hp, largo);
                int cDesp = r.CDesp, cCrec = r.CCrec;

                if (cDesp + cCrec == 0)
                {
                    foreach (var pid in priPoles) WriteZero(tr, pid);
                    continue;
                }

                // Ranking por midpoint del SEGMENTO (centrales primero) + sub-criterio binario ObservationCodes.
                Point3d? mid = GetCurveMidpoint(tr, db, segId);
                var obsCodes = AddinSettings.Current.ObservationCodes;

                var sortedPoles = priPoles
                    .Select(pid =>
                    {
                        double dist = mid.HasValue
                            ? Extensions.GetInsertionOrPosition(
                                  (Entity)tr.GetObject(pid, OpenMode.ForRead))
                                .DistanceTo(mid.Value)
                            : 0.0;
                        bool tieneObs = false;
                        if (obsCodes != null && obsCodes.Count > 0)
                        {
                            string csv = XDataManager.GetString(tr, pid, XDataKeys.COMENTARIOS) ?? string.Empty;
                            if (!string.IsNullOrEmpty(csv))
                            {
                                string[] tokens = csv.Split(',');
                                foreach (string token in tokens)
                                {
                                    string t = token.Trim();
                                    foreach (string code in obsCodes)
                                    {
                                        if (string.Equals(t, code.Trim(), System.StringComparison.OrdinalIgnoreCase))
                                        {
                                            tieneObs = true;
                                            break;
                                        }
                                    }
                                    if (tieneObs) break;
                                }
                            }
                        }
                        return new { Id = pid, Dist = dist, TieneObs = tieneObs };
                    })
                    .OrderBy(x => x.TieneObs ? 1 : 0)
                    .ThenBy(x => x.Dist)
                    .Select(x => x.Id)
                    .ToList();

                var sequence = BuildInterleavedSequence(cDesp, cCrec);

                int polesToUse = Math.Min(sortedPoles.Count, sequence.Count);
                var assign = new Dictionary<ObjectId, (int d, int c)>();
                for (int i = 0; i < sequence.Count; i++)
                {
                    ObjectId pole = sortedPoles[i % polesToUse];
                    assign.TryGetValue(pole, out var cur);
                    assign[pole] = sequence[i]
                        ? (cur.d + 1, cur.c)
                        : (cur.d,     cur.c + 1);
                }

                foreach (var pid in priPoles)
                {
                    if (assign.TryGetValue(pid, out var pc))
                    {
                        XDataManager.SetValues(tr, pid, new (string, object)[]
                        {
                            (XDataKeys.C_DESP, (object)pc.d),
                            (XDataKeys.C_CREC, (object)pc.c),
                        });
                    }
                    else
                    {
                        WriteZero(tr, pid);
                    }
                }

                stats.Segmentos++;
                stats.TotalCtos += cDesp + cCrec;

                logInfo?.Invoke(
                    $"  Segmento {ShortHandle(segId)} HP={hp} L={largo:F0}m → " +
                    $"D={cDesp} C={cCrec} (postes PRI={priPoles.Count}, usados={polesToUse})");
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static void WriteZero(Transaction tr, ObjectId pid)
        {
            XDataManager.SetValues(tr, pid, new (string, object)[]
            {
                (XDataKeys.C_DESP, (object)0),
                (XDataKeys.C_CREC, (object)0),
            });
        }

        /// <summary>
        /// Devuelve el LARGO_FRENTE del ID_FRENTE mayoritario entre los priPoles.
        /// Si hay 2+ frentes distintos, loggea warning navegable (handle del segmento).
        /// </summary>
        private static double PickLargoFrenteByMajority(
            Transaction tr, List<ObjectId> priPoles, string segHandleHex, Action<string> logInfo)
        {
            var counts = new Dictionary<string, (int n, double largo)>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var pid in priPoles)
            {
                string idFrente = XDataManager.GetString(tr, pid, XDataKeys.ID_FRENTE) ?? string.Empty;
                double lf       = XDataManager.GetReal  (tr, pid, XDataKeys.LARGO_FRENTE) ?? 0.0;
                if (string.IsNullOrEmpty(idFrente) || lf <= 0.0) continue;

                counts.TryGetValue(idFrente, out var acc);
                counts[idFrente] = (acc.n + 1, lf);
            }

            if (counts.Count == 0) return 0.0;

            KeyValuePair<string, (int n, double largo)> best = default;
            bool found = false;
            foreach (var kv in counts)
            {
                if (!found || kv.Value.n > best.Value.n) { best = kv; found = true; }
            }

            if (counts.Count > 1)
            {
                logInfo?.Invoke(
                    $"  ⚠ Segmento <H:{segHandleHex}> tiene postes PRIORIDAD en {counts.Count} frentes " +
                    $"distintos. Usando frente mayoritario {ShortHandle(best.Key)} (n={best.Value.n}). " +
                    $"CTO_ZOOM_HANDLE {segHandleHex}");
            }

            return best.Value.largo;
        }

        /// <summary>
        /// D,C,D,C,... (D=3,C=1 → D,C,D,D). true=DESP, false=CREC.
        /// </summary>
        private static List<bool> BuildInterleavedSequence(int cDesp, int cCrec)
        {
            var seq = new List<bool>(cDesp + cCrec);
            int d = cDesp, c = cCrec;
            while (d > 0 || c > 0)
            {
                if (d > 0) { seq.Add(true);  d--; }
                if (c > 0) { seq.Add(false); c--; }
            }
            return seq;
        }

        private static Point3d? GetCurveMidpoint(Transaction tr, Database db, string handleHex)
        {
            if (string.IsNullOrEmpty(handleHex)) return null;
            if (!long.TryParse(handleHex, NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture, out long hv)) return null;
            if (!db.TryGetObjectId(new Handle(hv), out ObjectId id)) return null;

            Curve c = tr.GetObject(id, OpenMode.ForRead) as Curve;
            if (c == null) return null;
            return c.StartPoint + (c.EndPoint - c.StartPoint) * 0.5;
        }

        private static string ShortHandle(string h) =>
            string.IsNullOrEmpty(h) ? "?" : h.Substring(0, Math.Min(h.Length, 6));
    }
}
