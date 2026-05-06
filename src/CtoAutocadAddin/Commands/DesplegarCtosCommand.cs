using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Koovra.Cto.AutocadAddin.Geometry;
using Koovra.Cto.AutocadAddin.Infrastructure;
using Koovra.Cto.AutocadAddin.Models;
using Koovra.Cto.AutocadAddin.Persistence;
using Koovra.Cto.AutocadAddin.Map;
using Koovra.Cto.AutocadAddin.Services;
using Koovra.Cto.Core;

namespace Koovra.Cto.AutocadAddin.Commands
{
    public class DesplegarCtosCommand
    {
        [CommandMethod("CTO_DESPLEGAR", CommandFlags.Modal)]
        public void Execute()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db  = doc.Database;

            if (!SelectionContext.Instance.TryGetPostes(out var polesIds))
            {
                AcadLogger.Warn("No hay postes seleccionados. Ejecuta CTO_SELECCIONAR_POSTES primero.");
                return;
            }

            AddinSettings s = AddinSettings.Current;
            AcadLogger.Info($"Desplegando: Acceso='{s.BlockNameDesp}'  Crecimiento='{s.BlockNameCrec}'  Capa='{s.CtoLayerName}'");

            Editor ed = doc.Editor;
            int total = 0;

            var odQueue = new System.Collections.Generic.List<System.Tuple<ObjectId, int>>();

            using (doc.LockDocument())
            {
            ObjectDataWriter.EnsureTable();

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {

                var deployer = new CtoBlockDeployer(s.BlockNameDesp, s.BlockNameCrec,
                                                    s.CtoLayerNameDesp, s.CtoLayerNameCrec);
                int purged = deployer.PurgeExistingBlocks(tr, db);
                if (purged > 0) AcadLogger.Info($"Purga previa: {purged} bloques en capas '{s.CtoLayerNameDesp}'/'{s.CtoLayerNameCrec}' eliminados.");
                int purgedCircles = deployer.PurgeAlertCircles(tr, db);
                if (purgedCircles > 0) AcadLogger.Info($"Purga círculos alerta: {purgedCircles} círculos previos borrados.");
                // ── Agrupar por ID_SEGMENT para distribuir HP a nivel SEGMENTO ──
                var polesBySeg = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<ObjectId>>(
                    StringComparer.OrdinalIgnoreCase);
                var orphanPoles = new System.Collections.Generic.List<ObjectId>();
                foreach (ObjectId pid in polesIds)
                {
                    string segId = XDataManager.GetString(tr, pid, XDataKeys.ID_SEGMENT) ?? string.Empty;
                    if (string.IsNullOrEmpty(segId)) { orphanPoles.Add(pid); continue; }
                    if (!polesBySeg.TryGetValue(segId, out var list))
                        polesBySeg[segId] = list = new System.Collections.Generic.List<ObjectId>();
                    list.Add(pid);
                }

                foreach (var kv in polesBySeg)
                {
                    var segPoles = kv.Value;
                    int hpSeg = 0;
                    int totalDesp = 0;
                    foreach (var pid in segPoles)
                    {
                        if (hpSeg == 0) hpSeg = XDataManager.GetInt(tr, pid, XDataKeys.HP) ?? 0;
                        totalDesp += XDataManager.GetInt(tr, pid, XDataKeys.C_DESP) ?? 0;
                    }
                    int[] allHp = HpDistributor.Distribute(hpSeg, totalDesp);

                    int offset = 0;
                    foreach (var pid in segPoles)
                    {
                        int cDespPole = XDataManager.GetInt(tr, pid, XDataKeys.C_DESP) ?? 0;
                        int[] slice = new int[cDespPole];
                        for (int i = 0; i < cDespPole && (offset + i) < allHp.Length; i++)
                            slice[i] = allHp[offset + i];
                        offset += cDespPole;
                        total += deployer.DeployForPole(tr, db, pid, slice, odQueue);
                    }
                }

                foreach (ObjectId pid in orphanPoles)
                {
                    int hp = XDataManager.GetInt(tr, pid, XDataKeys.HP) ?? 0;
                    int cd = XDataManager.GetInt(tr, pid, XDataKeys.C_DESP) ?? 0;
                    int[] slice = HpDistributor.Distribute(hp, cd);
                    total += deployer.DeployForPole(tr, db, pid, slice, odQueue);
                }

                // ── Overflow: cajas que no cupieron en postes → midpoint del segmento ──────
                // Paso 4 escribió C_DESP_OVF / C_CREC_OVF en el primer poste de cada segmento.
                foreach (var kv2 in polesBySeg)
                {
                    string segIdOvf    = kv2.Key;
                    var    segPolesOvf = kv2.Value;

                    // Leer overflow calculado por Paso 4 (primer poste del segmento)
                    ObjectId anchorPole = segPolesOvf[0];
                    int ovfD = XDataManager.GetInt(tr, anchorPole, XDataKeys.C_DESP_OVF) ?? 0;
                    int ovfC = XDataManager.GetInt(tr, anchorPole, XDataKeys.C_CREC_OVF) ?? 0;
                    if (ovfD + ovfC == 0) continue;

                    // HP del primer poste con HP > 0 (para slice)
                    int hpOvf = 0;
                    foreach (var pid2 in segPolesOvf)
                    {
                        hpOvf = XDataManager.GetInt(tr, pid2, XDataKeys.HP) ?? 0;
                        if (hpOvf > 0) break;
                    }

                    // Midpoint del segmento
                    Point3d midPt = Point3d.Origin;
                    bool midFound = false;
                    if (long.TryParse(segIdOvf, System.Globalization.NumberStyles.HexNumber,
                            System.Globalization.CultureInfo.InvariantCulture, out long hv2)
                        && db.TryGetObjectId(new Handle(hv2), out ObjectId segCurveId2))
                    {
                        var segCurveOvf = tr.GetObject(segCurveId2, OpenMode.ForRead) as Curve;
                        if (segCurveOvf != null)
                        {
                            Vector3d rawDir2 = segCurveOvf.EndPoint - segCurveOvf.StartPoint;
                            midPt = segCurveOvf.StartPoint + rawDir2 * 0.5;
                            midFound = true;
                        }
                    }
                    if (!midFound)
                    {
                        // Fallback: centroide de los postes del segmento
                        double sx = 0, sy = 0, sz = 0;
                        foreach (var pid2 in segPolesOvf)
                        {
                            var ent2 = tr.GetObject(pid2, OpenMode.ForRead) as Entity;
                            Point3d p2 = Extensions.GetInsertionOrPosition(ent2);
                            sx += p2.X; sy += p2.Y; sz += p2.Z;
                        }
                        int n2 = segPolesOvf.Count;
                        midPt = new Point3d(sx / n2, sy / n2, sz / n2);
                    }

                    int[] hpOvfSlice = HpDistributor.Distribute(hpOvf, ovfD);
                    int placed = deployer.DeployAtPoint(tr, db, midPt, ovfD, ovfC, hpOvfSlice, odQueue);
                    if (placed > 0) deployer.DrawAlertCircle(tr, db, midPt);
                    AcadLogger.Info($"Overflow seg {segIdOvf.Substring(0, Math.Min(segIdOvf.Length, 6))}: " +
                                    $"{ovfD}D+{ovfC}C → midpoint ({midPt.X:F0},{midPt.Y:F0}) placed={placed}.");
                    total += placed;
                }

                // ── Detectar CONT_HP sin postes en segmento → desplegar al midpoint ───
                // Pasamos los segmentIds para que LoadAllHpBlocks asocie cada CONT_HP a su
                // segmento. Sin esto, SegmentId queda null y el midpoint no se puede resolver.
                ObjectIdCollection segmentos = SelectionContext.Instance.Segmentos;
                if (segmentos == null || segmentos.Count == 0)
                    segmentos = SelectionService.SelectSegmentos(ed);
                var hpBlocks = TextBufferCollector.LoadAllHpBlocks(tr, ed, segmentos);
                AcadLogger.Info($"CONT_HP cargados: {hpBlocks.Count} (con segmento asociado).");

                // Pre-cargar posiciones de postes para no re-abrir en cada iteración
                var polePositions = new System.Collections.Generic.List<System.Tuple<ObjectId, Point3d>>();
                foreach (ObjectId pid in polesIds)
                {
                    var poleEnt = tr.GetObject(pid, OpenMode.ForRead) as Entity;
                    if (poleEnt != null)
                        polePositions.Add(System.Tuple.Create(pid, Extensions.GetInsertionOrPosition(poleEnt)));
                }

                foreach (var hpBlock in hpBlocks)
                {
                    // Cobertura por SEGMENTO: si el segmento del CONT_HP tiene postes con cajas
                    // (en polesBySeg), está cubierto. Si no (segmento sin postes en selección),
                    // hay que desplegar al midpoint.
                    // Antes se usaba un check espacial de 100m que confundía CONT_HP de un segmento
                    // con postes de OTRO segmento cercano y suprimía el deploy al midpoint.
                    bool tieneCapas = false;
                    if (!string.IsNullOrEmpty(hpBlock.SegmentId)
                        && polesBySeg.TryGetValue(hpBlock.SegmentId, out var polesOfSeg))
                    {
                        foreach (var pid3 in polesOfSeg)
                        {
                            int cDesp = XDataManager.GetInt(tr, pid3, XDataKeys.C_DESP) ?? 0;
                            int cCrec = XDataManager.GetInt(tr, pid3, XDataKeys.C_CREC) ?? 0;
                            if (cDesp + cCrec > 0) { tieneCapas = true; break; }
                        }
                    }
                    if (!tieneCapas)
                    {
                        double largo = 0.0;
                        Point3d insertPt = hpBlock.Position; // fallback: posición del texto HP
                        if (!string.IsNullOrEmpty(hpBlock.SegmentId)
                            && long.TryParse(hpBlock.SegmentId, System.Globalization.NumberStyles.HexNumber,
                                             System.Globalization.CultureInfo.InvariantCulture, out long hv)
                            && db.TryGetObjectId(new Handle(hv), out ObjectId segId))
                        {
                            var segCurve = tr.GetObject(segId, OpenMode.ForRead) as Curve;
                            if (segCurve != null)
                            {
                                try { largo = segCurve.GetDistanceAtParameter(segCurve.EndParam); }
                                catch { largo = segCurve.StartPoint.DistanceTo(segCurve.EndPoint); }
                                // Midpoint del segmento
                                Vector3d segDir3 = segCurve.EndPoint - segCurve.StartPoint;
                                insertPt = segCurve.StartPoint + segDir3 * 0.5;
                            }
                        }

                        var r = CtoCountCalculator.Calculate(hpBlock.Hp, largo);
                        if (r.CDesp + r.CCrec > 0)
                        {
                            int[] hpSlice = HpDistributor.Distribute(hpBlock.Hp, r.CDesp);
                            int placed2 = deployer.DeployAtPoint(tr, db, insertPt,
                                r.CDesp, r.CCrec, hpSlice, odQueue, hpBlock.Rotation);
                            if (placed2 > 0) deployer.DrawAlertCircle(tr, db, insertPt);
                            total += placed2;
                            AcadLogger.Info($"Sin postes: HP={hpBlock.Hp} seg={hpBlock.SegmentId?.Substring(0, Math.Min(hpBlock.SegmentId?.Length ?? 0, 6))} → {r.CDesp}D+{r.CCrec}C en midpoint ({insertPt.X:F0},{insertPt.Y:F0}) rot={hpBlock.Rotation:F2} placed={placed2}.");
                        }
                    }
                }

                tr.Commit();
            }

            // ── Segunda pasada: escribir OD en entidades ya commiteadas ──────────
            AcadLogger.Info($"OD queue: {odQueue.Count} bloques a procesar.");
            if (odQueue.Count > 0)
            {
                try
                {
                    using (Transaction trOd = db.TransactionManager.StartTransaction())
                    {
                        int ok = 0;
                        foreach (var entry in odQueue)
                        {
                            ObjectDataWriter.WriteCajaAcceso(entry.Item1, entry.Item2);
                            ok++;
                            if (ok == 1) AcadLogger.Info($"OD: primer bloque procesado ({entry.Item1}).");
                        }
                        trOd.Commit();
                        AcadLogger.Info($"OD CAJA_ACCESO escrito en {ok} bloques.");
                    }
                }
                catch (System.Exception ex)
                {
                    AcadLogger.Warn($"OD segunda pasada FALLÓ: {ex.GetType().FullName} — {ex.Message}");
                    if (ex.InnerException != null)
                        AcadLogger.Warn($"OD InnerException: {ex.InnerException.GetType().FullName} — {ex.InnerException.Message}");
                }
            }

            } // end LockDocument

            AcadLogger.Info($"{total} bloques CTO desplegados.");
        }
    }
}
