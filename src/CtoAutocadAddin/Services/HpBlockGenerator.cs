using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Koovra.Cto.AutocadAddin.Persistence;

namespace Koovra.Cto.AutocadAddin.Services
{
    /// <summary>
    /// Inserta y purga bloques CONT_HP calculados a partir de las propiedades servicio.
    ///
    /// Los generados van a la MISMA capa que los dibujados a mano ("Conteo HP"), porque el
    /// paso 3 los lee filtrando por esa capa. Por eso la idempotencia no puede ser por capa:
    /// se marcan con XData ORIGEN=AUTO y solo se purgan los marcados. Un CONT_HP relevado a
    /// mano nunca se toca — el dato de campo le gana a la estimación.
    /// </summary>
    public class HpBlockGenerator
    {
        public const string CapaConteoHp = "Conteo HP";
        public const string NombreBloque = "CONT_HP";
        public const string TagSdu       = "SDU";
        public const string TagMdu       = "MDU";

        /// <summary>Borra solo los CONT_HP marcados como generados. Devuelve cuántos borró.</summary>
        public int PurgeGenerated(Transaction tr, Database db)
        {
            int purged = 0;
            var blkTbl = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);

            foreach (ObjectId btrId in blkTbl)
            {
                var btr = tr.GetObject(btrId, OpenMode.ForRead) as BlockTableRecord;
                if (btr == null || !btr.IsLayout) continue;

                foreach (ObjectId id in btr)
                {
                    var br = tr.GetObject(id, OpenMode.ForRead) as BlockReference;
                    if (br == null) continue;
                    if (!string.Equals(br.Layer, CapaConteoHp, StringComparison.OrdinalIgnoreCase)) continue;

                    string origen = XDataManager.GetString(tr, id, XDataKeys.ORIGEN);
                    if (!string.Equals(origen, XDataKeys.ORIGEN_AUTO, StringComparison.OrdinalIgnoreCase))
                        continue;

                    br.UpgradeOpen();
                    br.Erase();
                    purged++;
                }
            }

            return purged;
        }

        /// <summary>
        /// Segmentos que ya tienen un CONT_HP dibujado a mano (sin la marca ORIGEN=AUTO).
        /// Esos quedan excluidos de la generación para que el paso 3 no sume dos veces.
        /// Llamar DESPUÉS de <see cref="PurgeGenerated"/>.
        /// </summary>
        public HashSet<string> FindManualSegments(
            Transaction tr, Editor ed, ObjectIdCollection segmentos)
        {
            var segs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            ObjectIdCollection ids = SelectionService.SelectAllOnLayer(ed, CapaConteoHp, "INSERT");
            foreach (ObjectId id in ids)
            {
                var br = tr.GetObject(id, OpenMode.ForRead) as BlockReference;
                if (br == null) continue;

                string origen = XDataManager.GetString(tr, id, XDataKeys.ORIGEN);
                if (string.Equals(origen, XDataKeys.ORIGEN_AUTO, StringComparison.OrdinalIgnoreCase))
                    continue;   // generado por nosotros en una corrida anterior no purgada

                string segId = TextBufferCollector.FindNearestSegmentHandle(tr, br.Position, segmentos);
                if (!string.IsNullOrEmpty(segId)) segs.Add(segId);
            }

            return segs;
        }

        /// <summary>
        /// Inserta un CONT_HP con SDU = <paramref name="sdu"/> y MDU = 0, marcado como generado.
        /// Lanza si el bloque no existe en el DWG o no tiene los atributos esperados.
        /// </summary>
        public ObjectId Insert(
            Transaction tr, Database db, Point3d position, double rotation,
            int sdu, string segHandleHex)
        {
            var blkTbl = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            if (!blkTbl.Has(NombreBloque))
                throw new InvalidOperationException(
                    $"El bloque '{NombreBloque}' no existe en el DWG. Insertalo una vez desde el template FTTH.");

            ObjectId defId = blkTbl[NombreBloque];
            if (!BlockAttributeWriter.HasAttributeDefinitions(tr, defId))
                throw new InvalidOperationException(
                    $"El bloque '{NombreBloque}' del DWG no tiene atributos ({TagSdu}/{TagMdu}). " +
                    "Sin ellos el paso 3 leería HP=0 en silencio.");

            EnsureLayer(tr, db, CapaConteoHp);

            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

            ObjectId newId;
            using (var br = new BlockReference(position, defId))
            {
                br.Rotation = rotation;
                br.Layer    = CapaConteoHp;
                ms.AppendEntity(br);
                tr.AddNewlyCreatedDBObject(br, true);

                BlockAttributeWriter.AppendAttributes(tr, br, new Dictionary<string, string>
                {
                    { TagSdu, sdu.ToString() },
                    { TagMdu, "0" },
                });

                newId = br.ObjectId;
            }

            XDataManager.SetValues(tr, newId, new (string, object)[]
            {
                (XDataKeys.ID_SEGMENT, segHandleHex ?? string.Empty),
                (XDataKeys.ORIGEN,     XDataKeys.ORIGEN_AUTO),
            });

            return newId;
        }

        private static void EnsureLayer(Transaction tr, Database db, string layerName)
        {
            var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (layerTable.Has(layerName)) return;
            layerTable.UpgradeOpen();
            using (var lr = new LayerTableRecord { Name = layerName })
            {
                layerTable.Add(lr);
                tr.AddNewlyCreatedDBObject(lr, true);
            }
        }
    }
}
