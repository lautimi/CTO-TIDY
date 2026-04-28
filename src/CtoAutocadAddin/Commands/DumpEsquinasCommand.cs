using System;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Koovra.Cto.AutocadAddin.Geometry;
using Koovra.Cto.AutocadAddin.Infrastructure;
using Koovra.Cto.AutocadAddin.Map;
using Koovra.Cto.AutocadAddin.Services;

namespace Koovra.Cto.AutocadAddin.Commands
{
    /// <summary>
    /// Debug: construye StreetCornerLibrary y dibuja las esquinas detectadas
    /// como círculos sobre la capa CTO_AUDIT_ESQUINAS.
    /// </summary>
    public class DumpEsquinasCommand
    {
        public const string LAYER_AUDIT_ESQUINAS = "CTO_AUDIT_ESQUINAS";
        public const short  COLOR_AMARILLO = 2;
        public const double CIRCLE_RADIUS = 0.5;

        [CommandMethod("CTO_DUMP_ESQUINAS")]
        public void Execute()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;

            var pso = new PromptSelectionOptions
            {
                MessageForAdding = "\nSeleccioná segmentos de calle: "
            };
            var psr = ed.GetSelection(pso);
            if (psr.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\nCTO_DUMP_ESQUINAS cancelado.");
                return;
            }

            var ids = new ObjectIdCollection();
            foreach (SelectedObject so in psr.Value)
                if (so != null) ids.Add(so.ObjectId);

            var calleByOid = ObjectDataReader.ReadCalle1Bulk(ids);
            ed.WriteMessage($"\n[CTO_DUMP_ESQUINAS] {calleByOid.Count}/{ids.Count} segmentos con CALLE_1.\n");

            Database db = doc.Database;
            using (doc.LockDocument())
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                var lib = StreetCornerLibrary.Build(tr, calleByOid);
                ed.WriteMessage($"[CTO_DUMP_ESQUINAS] Esquinas: {lib.CornerCount} | Calles distintas: {lib.StreetCount}\n");

                EnsureLayer(tr, db, LAYER_AUDIT_ESQUINAS, COLOR_AMARILLO);
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                foreach (var c in lib.All)
                {
                    var circ = new Circle(c.Point, Vector3d.ZAxis, CIRCLE_RADIUS)
                    {
                        Layer = LAYER_AUDIT_ESQUINAS,
                        ColorIndex = COLOR_AMARILLO,
                    };
                    ms.AppendEntity(circ);
                    tr.AddNewlyCreatedDBObject(circ, true);

                    var text = new DBText
                    {
                        Position = new Point3d(c.Point.X + CIRCLE_RADIUS, c.Point.Y + CIRCLE_RADIUS, c.Point.Z),
                        TextString = $"{c.CalleA} x {c.CalleB}",
                        Height = 1.0,
                        Layer = LAYER_AUDIT_ESQUINAS,
                        ColorIndex = COLOR_AMARILLO,
                    };
                    ms.AppendEntity(text);
                    tr.AddNewlyCreatedDBObject(text, true);
                }

                tr.Commit();
            }

            ed.WriteMessage($"[CTO_DUMP_ESQUINAS] Dibujado en capa {LAYER_AUDIT_ESQUINAS}.\n");
            AcadLogger.Info($"CTO_DUMP_ESQUINAS completado.");
        }

        private static void EnsureLayer(Transaction tr, Database db, string name, short colorIdx)
        {
            LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (lt.Has(name)) return;
            lt.UpgradeOpen();
            var ltr = new LayerTableRecord
            {
                Name = name,
                Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, colorIdx),
            };
            lt.Add(ltr);
            tr.AddNewlyCreatedDBObject(ltr, true);
        }
    }
}
