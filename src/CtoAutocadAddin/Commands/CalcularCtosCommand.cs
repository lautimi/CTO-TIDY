using System;
using System.Collections.Generic;
using System.Globalization;
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
    ///   1. Separar postes del segmento en PRIORIDAD vs SECUNDARIA vs central.
    ///   2. HP se toma del primer poste PRIORIDAD (o del primero del segmento si no hay PRI).
    ///   3. LARGO_FRENTE se toma por voto mayoritario de los postes PRIORIDAD (o todos si no hay PRI).
    ///   4. Calcular C_DESP, C_CREC con la tabla oficial (HP × LARGO_FRENTE).
    ///   5. Ordenar postes: PRIORIDAD → SECUNDARIA → central, cada grupo por posición lineal.
    ///   6. Generar secuencia D,C,D,C,... y distribuir round-robin.
    ///   7. Escribir C_DESP/C_CREC en XData para TODOS los postes del segmento.
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

                // ── Separar postes por tipo de linga ─────────────────────────────
                var priPoles     = new List<ObjectId>();
                var secPoles     = new List<ObjectId>();
                var centralPoles = new List<ObjectId>();
                foreach (var pid in polesAll)
                {
                    string tipo = XDataManager.GetString(tr, pid, XDataKeys.LINGA_TIPO) ?? string.Empty;
                    if (string.Equals(tipo, XDataKeys.LINGA_PRIORIDAD, StringComparison.OrdinalIgnoreCase))
                        priPoles.Add(pid);
                    else if (string.Equals(tipo, XDataKeys.LINGA_SECUNDARIA, StringComparison.OrdinalIgnoreCase))
                        secPoles.Add(pid);
                    else
                        centralPoles.Add(pid);
                }

                // HP del segmento: tomar del primer poste PRIORIDAD, o del primer poste del segmento.
                var hpSource = priPoles.Count > 0 ? priPoles[0] : polesAll[0];
                int hp = XDataManager.GetInt(tr, hpSource, XDataKeys.HP) ?? 0;

                // LARGO_FRENTE por voto mayoritario entre postes PRIORIDAD (o todos si no hay PRI).
                var largoSource = priPoles.Count > 0 ? priPoles : polesAll;
                double largo = PickLargoFrenteByMajority(tr, largoSource, segId, logInfo);

                if (largo <= 0.0)
                {
                    foreach (var pid in polesAll) WriteZero(tr, pid);
                    logInfo?.Invoke(
                        $"  ⚠ Segmento {ShortHandle(segId)} sin LARGO_FRENTE resuelto — " +
                        $"{polesAll.Count} postes forzados a 0,0");
                    continue;
                }

                if (CtoCountCalculator.IsOutOfRange(hp)) stats.OutOfRange++;
                CtoCountCalculator.Result r = CtoCountCalculator.Calculate(hp, largo);
                int cDesp = r.CDesp, cCrec = r.CCrec;

                if (cDesp + cCrec == 0)
                {
                    foreach (var pid in polesAll) WriteZero(tr, pid);
                    continue;
                }

                // ── Geometría del segmento: dirección lineal y midpoint ───────────
                Curve segCurve = null;
                if (!string.IsNullOrEmpty(segId))
                {
                    if (long.TryParse(segId, NumberStyles.HexNumber,
                            CultureInfo.InvariantCulture, out long hv))
                    {
                        if (db.TryGetObjectId(new Handle(hv), out ObjectId cid))
                            segCurve = tr.GetObject(cid, OpenMode.ForRead) as Curve;
                    }
                }
                Vector3d segDir    = Vector3d.XAxis;
                Point3d  segOrigin = Point3d.Origin;
                Point3d  segMid    = Point3d.Origin;
                if (segCurve != null)
                {
                    Vector3d rawDir = segCurve.EndPoint - segCurve.StartPoint;
                    if (rawDir.Length > 0.001) { segDir = rawDir.GetNormal(); }
                    segOrigin = segCurve.StartPoint;
                    segMid    = segCurve.StartPoint + rawDir * 0.5;
                }
                else
                {
                    // Sin curva: usar centroide de los postes como midpoint
                    double sx = 0, sy = 0, sz = 0;
                    foreach (var pid in polesAll)
                    {
                        var ent2 = tr.GetObject(pid, OpenMode.ForRead) as Entity;
                        Point3d p = Extensions.GetInsertionOrPosition(ent2);
                        sx += p.X; sy += p.Y; sz += p.Z;
                    }
                    int n = polesAll.Count;
                    segMid = new Point3d(sx / n, sy / n, sz / n);
                }

                var obsCodes = AddinSettings.Current.ObservationCodes ?? new List<string>();

                // ── Paso 1: ordenar cada grupo por centralidad (más cercanos al mid primero) ──
                var priCentral     = SortByCentrality(priPoles,     tr, segMid, obsCodes);
                var secCentral     = SortByCentrality(secPoles,     tr, segMid, obsCodes);
                var centralCentral = SortByCentrality(centralPoles, tr, segMid, obsCodes);

                // ── Paso 2: concatenar respetando prioridad y tomar los N más centrales ──
                var candidates = new List<ObjectId>();
                candidates.AddRange(priCentral);
                candidates.AddRange(secCentral);
                candidates.AddRange(centralCentral);

                // Secuencia interleaved D,C,D,C,...
                var sequence   = BuildInterleavedSequence(cDesp, cCrec);
                // Usamos tantos postes como items en la secuencia (spread máximo),
                // pero nunca más que los candidatos disponibles.
                int polesToUse = Math.Min(candidates.Count, sequence.Count);
                var selected   = candidates.GetRange(0, polesToUse);

                // ── Paso 3: reordenar los seleccionados por posición lineal → D-C-D visual ──
                var sortedPoles = SortByLinearPosition(selected, tr, segDir, segOrigin, new List<string>());

                // ── Diagnóstico: loguear postes seleccionados cuando hay priPoles ──
                if (priPoles.Count > 0)
                {
                    foreach (var pid in sortedPoles)
                    {
                        string tipo2   = XDataManager.GetString(tr, pid, XDataKeys.LINGA_TIPO) ?? "(sin linga)";
                        string segPole = XDataManager.GetString(tr, pid, XDataKeys.ID_SEGMENT) ?? "?";
                        var ent3 = tr.GetObject(pid, OpenMode.ForRead) as Entity;
                        Point3d pos3 = Extensions.GetInsertionOrPosition(ent3);
                        logInfo?.Invoke($"    SELECCIONADO pid={pid.Handle} tipo='{tipo2}' seg='{ShortHandle(segPole)}' pos=({pos3.X:F0},{pos3.Y:F0})");
                    }
                }

                // ── Paso 4: round-robin D,C,D,C,... con cap 1D+1C por poste ─────
                // Si el round-robin intenta agregar un 2do D o 2do C al mismo poste,
                // el item queda como overflow. Paso 5 lo lee de C_DESP_OVF / C_CREC_OVF
                // y lo despliega en el midpoint del segmento.
                var assign = new Dictionary<ObjectId, (int d, int c)>();
                int ovfD = 0, ovfC = 0;
                for (int i = 0; i < sequence.Count; i++)
                {
                    ObjectId pole = sortedPoles[i % polesToUse];
                    assign.TryGetValue(pole, out var cur);
                    bool isD = sequence[i];
                    if (isD  && cur.d == 0) assign[pole] = (1, cur.c);
                    else if (!isD && cur.c == 0) assign[pole] = (cur.d, 1);
                    else if (isD) ovfD++;
                    else          ovfC++;
                }

                // ── Escribir resultados a TODOS los postes del segmento ───────────
                foreach (ObjectId pid in polesAll)
                {
                    assign.TryGetValue(pid, out var val);
                    XDataManager.SetValues(tr, pid, new (string, object)[]
                    {
                        (XDataKeys.C_DESP, (object)val.d),
                        (XDataKeys.C_CREC, (object)val.c),
                    });
                }

                // ── Escribir overflow en el primer poste del segmento (Paso 5 lo lee) ──
                XDataManager.SetValues(tr, polesAll[0], new (string, object)[]
                {
                    (XDataKeys.C_DESP_OVF, (object)ovfD),
                    (XDataKeys.C_CREC_OVF, (object)ovfC),
                });
                if (ovfD + ovfC > 0)
                    logInfo?.Invoke($"  Segmento {ShortHandle(segId)}: overflow {ovfD}D+{ovfC}C → se colocarán en midpoint en Paso 5.");

                stats.Segmentos++;
                stats.TotalCtos += cDesp + cCrec;

                logInfo?.Invoke(
                    $"  Segmento {ShortHandle(segId)} HP={hp} L={largo:F0}m → " +
                    $"D={cDesp} C={cCrec} (PRI={priPoles.Count} SEC={secPoles.Count} CTR={centralPoles.Count} usados={polesToUse})");
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
        /// Intercala D y C empezando por la mayoría para maximizar la distribución visual.
        /// Ejemplos: D=1,C=2 → C,D,C  |  D=2,C=1 → D,C,D  |  D=2,C=3 → C,D,C,D,C
        /// true=DESP, false=CREC.
        /// </summary>
        private static List<bool> BuildInterleavedSequence(int cDesp, int cCrec)
        {
            var seq = new List<bool>(cDesp + cCrec);
            int d = cDesp, c = cCrec;
            // Empieza por la mayoría (o D si iguales)
            bool turnD = (d >= c);
            while (d > 0 || c > 0)
            {
                if (turnD && d > 0)
                {
                    seq.Add(true);  d--; turnD = false;
                }
                else if (!turnD && c > 0)
                {
                    seq.Add(false); c--; turnD = true;
                }
                else if (d > 0) { seq.Add(true);  d--; }
                else            { seq.Add(false); c--; }
            }
            return seq;
        }

        /// <summary>
        /// Ordena postes de menor a mayor distancia al punto de referencia (midpoint del segmento).
        /// ObsCodes: postes con observación van al final (obsRank=1).
        /// </summary>
        private static List<ObjectId> SortByCentrality(
            List<ObjectId> poles,
            Transaction tr,
            Point3d midpoint,
            List<string> obsCodes)
        {
            if (poles == null || poles.Count == 0) return new List<ObjectId>();

            var entries = new List<Tuple<ObjectId, double, int>>();
            foreach (ObjectId pid in poles)
            {
                var ent = tr.GetObject(pid, OpenMode.ForRead) as Entity;
                Point3d pos = Extensions.GetInsertionOrPosition(ent);
                double dist = pos.DistanceTo(midpoint);

                int obsRank = 0;
                if (obsCodes != null && obsCodes.Count > 0)
                {
                    string csv = XDataManager.GetString(tr, pid, XDataKeys.COMENTARIOS) ?? string.Empty;
                    if (!string.IsNullOrEmpty(csv))
                    {
                        foreach (string token in csv.Split(','))
                        {
                            string t = token.Trim();
                            foreach (string code in obsCodes)
                            {
                                if (string.Equals(t, code.Trim(), StringComparison.OrdinalIgnoreCase))
                                { obsRank = 1; break; }
                            }
                            if (obsRank == 1) break;
                        }
                    }
                }
                entries.Add(Tuple.Create(pid, dist, obsRank));
            }

            entries.Sort((a, b) => {
                int c = a.Item3.CompareTo(b.Item3);
                if (c != 0) return c;
                return a.Item2.CompareTo(b.Item2);
            });

            var result = new List<ObjectId>(entries.Count);
            foreach (var e in entries) result.Add(e.Item1);
            return result;
        }

        /// <summary>
        /// Proyecta cada poste sobre el eje del segmento y ordena de menor a mayor proyección.
        /// ObsCodes: postes con observación van al final del grupo (obsRank=1).
        /// </summary>
        private static List<ObjectId> SortByLinearPosition(
            List<ObjectId> poles,
            Transaction tr,
            Vector3d segDir,
            Point3d segOrigin,
            List<string> obsCodes)
        {
            if (poles == null || poles.Count == 0) return new List<ObjectId>();

            var entries = new List<Tuple<ObjectId, double, int>>();
            foreach (ObjectId pid in poles)
            {
                var ent = tr.GetObject(pid, OpenMode.ForRead) as Entity;
                Point3d pos = Extensions.GetInsertionOrPosition(ent);
                double proj = segDir.DotProduct(pos - segOrigin);

                int obsRank = 0;
                if (obsCodes != null && obsCodes.Count > 0)
                {
                    string csv = XDataManager.GetString(tr, pid, XDataKeys.COMENTARIOS) ?? string.Empty;
                    if (!string.IsNullOrEmpty(csv))
                    {
                        foreach (string token in csv.Split(','))
                        {
                            string t = token.Trim();
                            foreach (string code in obsCodes)
                            {
                                if (string.Equals(t, code.Trim(), StringComparison.OrdinalIgnoreCase))
                                { obsRank = 1; break; }
                            }
                            if (obsRank == 1) break;
                        }
                    }
                }
                entries.Add(Tuple.Create(pid, proj, obsRank));
            }

            entries.Sort((a, b) => {
                int c = a.Item3.CompareTo(b.Item3);
                if (c != 0) return c;
                return a.Item2.CompareTo(b.Item2);
            });

            var result = new List<ObjectId>(entries.Count);
            foreach (var e in entries) result.Add(e.Item1);
            return result;
        }

        private static string ShortHandle(string h) =>
            string.IsNullOrEmpty(h) ? "?" : h.Substring(0, Math.Min(h.Length, 6));
    }
}
