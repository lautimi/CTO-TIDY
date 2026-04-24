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
using Koovra.Cto.AutocadAddin.Services;

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

            using (doc.LockDocument())
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                var deployer = new CtoBlockDeployer(s.BlockNameDesp, s.BlockNameCrec, s.CtoLayerName);
                int purged = deployer.PurgeExistingBlocks(tr, db);
                if (purged > 0) AcadLogger.Info($"Purga previa: {purged} bloques en capa '{s.CtoLayerName}' eliminados.");
                foreach (ObjectId poleId in polesIds)
                    total += deployer.DeployForPole(tr, db, poleId);

                // ── Purgar círculos de alerta anteriores (idempotencia) ────────
                var blkTbl2 = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                foreach (ObjectId btrId2 in blkTbl2)
                {
                    var btr2 = tr.GetObject(btrId2, OpenMode.ForRead) as BlockTableRecord;
                    if (btr2 == null || !btr2.IsLayout) continue;
                    foreach (ObjectId id2 in btr2)
                    {
                        var circ = tr.GetObject(id2, OpenMode.ForRead) as Circle;
                        if (circ == null) continue;
                        if (!string.Equals(circ.Layer, "0", StringComparison.OrdinalIgnoreCase)) continue;
                        if (Math.Abs(circ.Radius - GeometryConstants.CTO_ALERT_CIRCLE_RADIUS) > 0.001) continue;
                        circ.UpgradeOpen();
                        circ.Erase();
                    }
                }

                // ── Detectar CONT_HP sin cajas y dibujar círculos de alerta ───
                // Matching espacial: para cada CONT_HP, verificar si hay algún poste
                // con cajas (C_DESP+C_CREC>0) dentro de 100m. Si no hay → círculo rojo.
                // Esto cubre segmentos sin postes (SegmentId=null) que el matching por
                // ID-de-segmento anterior no detectaba.
                var hpBlocks = TextBufferCollector.LoadAllHpBlocks(tr, ed);
                var msWrite = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                // Pre-cargar posiciones de postes para no re-abrir en cada iteración
                var polePositions = new System.Collections.Generic.List<System.Tuple<ObjectId, Point3d>>();
                foreach (ObjectId pid in polesIds)
                {
                    var poleEnt = tr.GetObject(pid, OpenMode.ForRead) as Entity;
                    if (poleEnt != null)
                        polePositions.Add(System.Tuple.Create(pid, Extensions.GetInsertionOrPosition(poleEnt)));
                }

                const double hpMaxDist = 100.0;
                foreach (var hpBlock in hpBlocks)
                {
                    bool tieneCapas = false;
                    foreach (var poleEntry in polePositions)
                    {
                        if (hpBlock.Position.DistanceTo(poleEntry.Item2) > hpMaxDist) continue;
                        int cDesp = XDataManager.GetInt(tr, poleEntry.Item1, XDataKeys.C_DESP) ?? 0;
                        int cCrec = XDataManager.GetInt(tr, poleEntry.Item1, XDataKeys.C_CREC) ?? 0;
                        if (cDesp + cCrec > 0) { tieneCapas = true; break; }
                    }
                    if (!tieneCapas)
                    {
                        var alertCirc = new Circle(hpBlock.Position, Vector3d.ZAxis, GeometryConstants.CTO_ALERT_CIRCLE_RADIUS);
                        alertCirc.Layer = "0";
                        alertCirc.ColorIndex = 1; // rojo
                        msWrite.AppendEntity(alertCirc);
                        tr.AddNewlyCreatedDBObject(alertCirc, true);
                    }
                }

                tr.Commit();
            }

            AcadLogger.Info($"{total} bloques CTO desplegados.");
        }
    }
}
