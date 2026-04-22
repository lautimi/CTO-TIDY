using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Koovra.Cto.AutocadAddin.Geometry;
using Koovra.Cto.AutocadAddin.Models;
using Koovra.Cto.AutocadAddin.Persistence;

namespace Koovra.Cto.AutocadAddin.Services
{
    /// <summary>
    /// Inserta bloques CTO para cada poste:
    ///   - CAJA_ACCESO_b   × C_DESP  (Despliegue Inicial 40%)
    ///   - CAJA_CRECIMIENTO × C_CREC  (Crecimiento Futuro 100%)
    /// Rotación alineada al ángulo de la LINGA asociada (fallback: SEGMENTO).
    /// </summary>
    public class CtoBlockDeployer
    {
        private readonly string _blockNameDesp;
        private readonly string _blockNameCrec;
        private readonly string _layerName;

        public CtoBlockDeployer(string blockNameDesp, string blockNameCrec, string layerName)
        {
            _blockNameDesp = blockNameDesp;
            _blockNameCrec = blockNameCrec;
            _layerName     = layerName;
        }

        /// <summary>
        /// Borra todos los BlockReferences existentes en la capa CTO.
        /// Hace el deploy idempotente: correr Paso 5 N veces produce el mismo output
        /// que correrlo 1 sola vez (evita que cajas se apilen entre runs).
        /// Devuelve la cantidad de bloques borrados.
        /// </summary>
        public int PurgeExistingBlocks(Transaction tr, Database db)
        {
            int purged = 0;
            var blkTbl = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            foreach (ObjectId btrId in blkTbl)
            {
                var btr = tr.GetObject(btrId, OpenMode.ForRead) as BlockTableRecord;
                if (btr == null || btr.IsLayout == false) continue;
                foreach (ObjectId id in btr)
                {
                    var br = tr.GetObject(id, OpenMode.ForRead) as BlockReference;
                    if (br == null) continue;
                    if (!string.Equals(br.Layer, _layerName, StringComparison.OrdinalIgnoreCase)) continue;

                    br.UpgradeOpen();
                    br.Erase();
                    purged++;
                }
            }
            return purged;
        }

        public int DeployForPole(Transaction tr, Database db, ObjectId poleId)
        {
            int cDesp = XDataManager.GetInt(tr, poleId, XDataKeys.C_DESP) ?? 0;
            int cCrec = XDataManager.GetInt(tr, poleId, XDataKeys.C_CREC) ?? 0;
            if (cDesp + cCrec <= 0) return 0;

            Entity poleEnt = tr.GetObject(poleId, OpenMode.ForRead) as Entity;
            if (poleEnt == null) return 0;
            Point3d polePoint = Extensions.GetInsertionOrPosition(poleEnt);

            double angleRad = ComputeDeploymentAngle(tr, db, poleId);

            var blkTable = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            EnsureLayer(tr, db, _layerName);

            if (cDesp > 0 && !blkTable.Has(_blockNameDesp))
                throw new InvalidOperationException($"El bloque '{_blockNameDesp}' no existe en el DWG.");
            if (cCrec > 0 && !blkTable.Has(_blockNameCrec))
                throw new InvalidOperationException($"El bloque '{_blockNameCrec}' no existe en el DWG.");

            ObjectId defIdDesp = cDesp > 0 ? blkTable[_blockNameDesp] : ObjectId.Null;
            ObjectId defIdCrec = cCrec > 0 ? blkTable[_blockNameCrec] : ObjectId.Null;

            var ms = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
            int inserted = 0;
            int slot = 0;

            // ── Intercalado D,C,D,C,...  (dentro del poste) ─────────────────
            int d = cDesp, c = cCrec;
            while (d > 0 || c > 0)
            {
                if (d > 0)
                {
                    InsertBlock(tr, ms, defIdDesp, polePoint, angleRad, slot++);
                    inserted++;
                    d--;
                }
                if (c > 0)
                {
                    InsertBlock(tr, ms, defIdCrec, polePoint, angleRad, slot++);
                    inserted++;
                    c--;
                }
            }

            return inserted;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private void InsertBlock(Transaction tr, BlockTableRecord ms,
                                 ObjectId defId, Point3d polePoint,
                                 double angleRad, int slot)
        {
            Vector3d localOffset = new Vector3d(
                GeometryConstants.CTO_OFFSET_X + slot * GeometryConstants.CTO_SEPARACION,
                GeometryConstants.CTO_OFFSET_Y,
                0);
            Vector3d worldOffset = localOffset.RotateBy(angleRad, Vector3d.ZAxis);
            Point3d insPt = polePoint + worldOffset;

            using (var br = new BlockReference(insPt, defId))
            {
                br.Rotation = angleRad;
                br.Layer    = _layerName;
                ms.AppendEntity(br);
                tr.AddNewlyCreatedDBObject(br, true);
            }
        }

        /// <summary>
        /// Recupera el ángulo de despliegue para el poste (en radianes).
        /// Prioridad: ID_LINGA (cable físico real) → ID_SEGMENT (eje abstracto, fallback).
        /// Ambos almacenan un Handle hex que resuelve a una Curve.
        /// </summary>
        private static double ComputeDeploymentAngle(Transaction tr, Database db, ObjectId poleId)
        {
            string handleHex = XDataManager.GetString(tr, poleId, XDataKeys.ID_LINGA);
            if (string.IsNullOrEmpty(handleHex))
                handleHex = XDataManager.GetString(tr, poleId, XDataKeys.ID_SEGMENT);
            if (string.IsNullOrEmpty(handleHex)) return 0.0;

            if (!long.TryParse(handleHex, System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out long handleValue))
                return 0.0;

            if (!db.TryGetObjectId(new Handle(handleValue), out ObjectId curveId)) return 0.0;

            Curve curve = tr.GetObject(curveId, OpenMode.ForRead) as Curve;
            if (curve == null) return 0.0;

            return Math.Atan2(curve.EndPoint.Y - curve.StartPoint.Y,
                              curve.EndPoint.X - curve.StartPoint.X);
        }

        /// <summary>
        /// Crea la capa si no existe en el DWG (evita eKeyNotFound al asignar br.Layer).
        /// </summary>
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
