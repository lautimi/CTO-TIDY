using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace Koovra.Cto.AutocadAddin.Services
{
    /// <summary>
    /// Dibuja y purga las líneas "spider" (trazabilidad visual propiedad↔segmento y
    /// propiedad↔caja) en sus capas dedicadas. Entregable permanente, no auditoría purgable.
    /// </summary>
    public static class SpiderDrawer
    {
        public const string LAYER_SPIDER_CONTEO    = "CTO_SPIDER_CONTEO";
        public const string LAYER_SPIDER_ACOMETIDA = "CTO_SPIDER_ACOMETIDA";
        public const short  COLOR_CONTEO    = 4;   // cian
        public const short  COLOR_ACOMETIDA = 3;   // verde
        public const short  COLOR_EXCEDIDO  = 1;   // rojo — propiedad por encima del cupo de la caja

        public class SpiderLine
        {
            public Point3d From;
            public Point3d To;
            public bool    Flagged;   // true => se dibuja con COLOR_EXCEDIDO en vez de colorIdx
        }

        public static void EnsureLayer(Transaction tr, Database db, string layerName, short colorIdx)
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (lt.Has(layerName)) return;
            lt.UpgradeOpen();
            var ltr = new LayerTableRecord
            {
                Name  = layerName,
                Color = Color.FromColorIndex(ColorMethod.ByAci, colorIdx),
            };
            lt.Add(ltr);
            tr.AddNewlyCreatedDBObject(ltr, true);
        }

        public static int PurgeLayer(Transaction tr, Database db, string layerName)
        {
            int purged = 0;
            var blkTbl = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            foreach (ObjectId btrId in blkTbl)
            {
                var btr = tr.GetObject(btrId, OpenMode.ForRead) as BlockTableRecord;
                if (btr == null || !btr.IsLayout) continue;
                foreach (ObjectId id in btr)
                {
                    var line = tr.GetObject(id, OpenMode.ForRead) as Line;
                    if (line == null) continue;
                    if (!string.Equals(line.Layer, layerName, StringComparison.OrdinalIgnoreCase)) continue;

                    line.UpgradeOpen();
                    line.Erase();
                    purged++;
                }
            }
            return purged;
        }

        public static int DrawLines(Transaction tr, Database db, string layerName, short colorIdx,
                                     IEnumerable<SpiderLine> lines)
        {
            EnsureLayer(tr, db, layerName, colorIdx);

            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

            int drawn = 0;
            foreach (SpiderLine sl in lines)
            {
                var e = new Line(sl.From, sl.To)
                {
                    Layer      = layerName,
                    ColorIndex = sl.Flagged ? COLOR_EXCEDIDO : colorIdx,
                };
                ms.AppendEntity(e);
                tr.AddNewlyCreatedDBObject(e, true);
                drawn++;
            }
            return drawn;
        }
    }
}
