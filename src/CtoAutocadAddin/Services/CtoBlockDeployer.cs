using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Koovra.Cto.AutocadAddin.Geometry;
using Koovra.Cto.AutocadAddin.Infrastructure;
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
        private readonly string _layerNameDesp;
        private readonly string _layerNameCrec;

        // Constructor (2 capas, una por tipo de caja).
        public CtoBlockDeployer(string blockNameDesp, string blockNameCrec,
                                string layerNameDesp, string layerNameCrec)
        {
            _blockNameDesp = blockNameDesp;
            _blockNameCrec = blockNameCrec;
            _layerNameDesp = layerNameDesp;
            _layerNameCrec = layerNameCrec;
        }

        // Constructor compatible: una sola capa para ambos tipos.
        public CtoBlockDeployer(string blockNameDesp, string blockNameCrec, string layerName)
            : this(blockNameDesp, blockNameCrec, layerName, layerName)
        {
        }

        /// <summary>
        /// Borra todos los BlockReferences existentes en las capas CTO (Desp + Crec).
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
                    bool match = string.Equals(br.Layer, _layerNameDesp, StringComparison.OrdinalIgnoreCase)
                              || string.Equals(br.Layer, _layerNameCrec, StringComparison.OrdinalIgnoreCase);
                    if (!match) continue;

                    br.UpgradeOpen();
                    br.Erase();
                    purged++;
                }
            }
            return purged;
        }

        /// <summary>
        /// Borra los círculos de alerta previos (capa "0", radio CTO_ALERT_CIRCLE_RADIUS).
        /// Idempotencia para los círculos dibujados en cajas overflow / sin postes.
        /// </summary>
        public int PurgeAlertCircles(Transaction tr, Database db)
        {
            int purged = 0;
            var blkTbl = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            foreach (ObjectId btrId in blkTbl)
            {
                var btr = tr.GetObject(btrId, OpenMode.ForRead) as BlockTableRecord;
                if (btr == null || !btr.IsLayout) continue;
                foreach (ObjectId id in btr)
                {
                    var circ = tr.GetObject(id, OpenMode.ForRead) as Circle;
                    if (circ == null) continue;
                    if (!string.Equals(circ.Layer, "0", StringComparison.OrdinalIgnoreCase)) continue;
                    if (Math.Abs(circ.Radius - GeometryConstants.CTO_ALERT_CIRCLE_RADIUS) > 0.001) continue;
                    circ.UpgradeOpen();
                    circ.Erase();
                    purged++;
                }
            }
            return purged;
        }

        /// <summary>
        /// Dibuja un círculo de alerta (capa "0", radio CTO_ALERT_CIRCLE_RADIUS) en el punto dado.
        /// Marca visualmente las cajas que no se pudieron acomodar en postes.
        /// </summary>
        public void DrawAlertCircle(Transaction tr, Database db, Point3d center)
        {
            var ms = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
            using (var circ = new Circle(center, Vector3d.ZAxis, GeometryConstants.CTO_ALERT_CIRCLE_RADIUS))
            {
                circ.Layer = "0";
                ms.AppendEntity(circ);
                tr.AddNewlyCreatedDBObject(circ, true);
            }
        }

        public int DeployForPole(Transaction tr, Database db, ObjectId poleId,
            int[] hpPorDespliegue = null,
            System.Collections.Generic.List<System.Tuple<ObjectId, int>> odQueue = null)
        {
            int cDesp = XDataManager.GetInt(tr, poleId, XDataKeys.C_DESP) ?? 0;
            int cCrec = XDataManager.GetInt(tr, poleId, XDataKeys.C_CREC) ?? 0;
            if (cDesp + cCrec <= 0) return 0;

            Entity poleEnt = tr.GetObject(poleId, OpenMode.ForRead) as Entity;
            if (poleEnt == null) return 0;
            Point3d polePoint = Extensions.GetInsertionOrPosition(poleEnt);

            var angles = ComputeDeploymentAngles(tr, db, poleId, polePoint);

            var blkTable = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            EnsureLayer(tr, db, _layerNameDesp);
            EnsureLayer(tr, db, _layerNameCrec);

            if (cDesp > 0 && !blkTable.Has(_blockNameDesp))
                throw new InvalidOperationException($"El bloque '{_blockNameDesp}' no existe en el DWG.");
            if (cCrec > 0 && !blkTable.Has(_blockNameCrec))
                throw new InvalidOperationException($"El bloque '{_blockNameCrec}' no existe en el DWG.");

            ObjectId defIdDesp = cDesp > 0 ? blkTable[_blockNameDesp] : ObjectId.Null;
            ObjectId defIdCrec = cCrec > 0 ? blkTable[_blockNameCrec] : ObjectId.Null;

            var ms = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
            int inserted = 0;
            int slot = 0;
            int dIdx = 0;

            // ── Intercalado D,C,D,C,...  (dentro del poste) ─────────────────
            // Si el poste tiene exactamente 1D+1C, C va en la misma X con +3.54m perpendicular.
            bool esPar1D1C = (cDesp == 1 && cCrec == 1);
            int d = cDesp, c = cCrec;
            while (d > 0 || c > 0)
            {
                if (d > 0)
                {
                    ObjectId newId = InsertBlock(tr, ms, defIdDesp, polePoint,
                                                 angles.displayAngle, angles.offsetAngle, slot, 0.0, _layerNameDesp);
                    if (!newId.IsNull)
                    {
                        int hp = (hpPorDespliegue != null && dIdx < hpPorDespliegue.Length)
                            ? hpPorDespliegue[dIdx]
                            : 0;
                        if (odQueue != null)
                            odQueue.Add(System.Tuple.Create(newId, hp));
                        dIdx++;
                    }
                    inserted++;
                    d--;
                    if (!esPar1D1C) slot++;
                }
                if (c > 0)
                {
                    double extraY = esPar1D1C ? GeometryConstants.CTO_CREC_OFFSET_ADICIONAL : 0.0;
                    ObjectId newId = InsertBlock(tr, ms, defIdCrec, polePoint,
                                                 angles.displayAngle, angles.offsetAngle, slot, extraY, _layerNameCrec);
                    if (!newId.IsNull && odQueue != null)
                        odQueue.Add(System.Tuple.Create(newId, 0));
                    inserted++;
                    c--;
                    slot++;
                }
            }

            return inserted;
        }

        public int DeployAtPoint(Transaction tr, Database db, Point3d point,
            int cDesp, int cCrec,
            int[] hpPorDespliegue = null,
            System.Collections.Generic.List<System.Tuple<ObjectId, int>> odQueue = null,
            double rotation = 0.0)
        {
            if (cDesp + cCrec <= 0) return 0;

            var blkTable = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            EnsureLayer(tr, db, _layerNameDesp);
            EnsureLayer(tr, db, _layerNameCrec);

            if (cDesp > 0 && !blkTable.Has(_blockNameDesp))
                throw new InvalidOperationException($"El bloque '{_blockNameDesp}' no existe en el DWG.");
            if (cCrec > 0 && !blkTable.Has(_blockNameCrec))
                throw new InvalidOperationException($"El bloque '{_blockNameCrec}' no existe en el DWG.");

            ObjectId defIdDesp = cDesp > 0 ? blkTable[_blockNameDesp] : ObjectId.Null;
            ObjectId defIdCrec = cCrec > 0 ? blkTable[_blockNameCrec] : ObjectId.Null;

            var ms = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
            int inserted = 0;
            int slot = 0;
            int dIdx = 0;

            // Cajas overflow al midpoint: misma rotación que el bloque CONT_HP del segmento.
            // El offsetAngle alinea la fila slot (eje X local) con la dirección del CONT_HP.
            // Aplica también el par 1D+1C (D y C en mismo slot, C con offset perpendicular extra).
            bool esPar1D1C = (cDesp == 1 && cCrec == 1);
            int d = cDesp, c = cCrec;
            while (d > 0 || c > 0)
            {
                if (d > 0)
                {
                    ObjectId newId = InsertBlock(tr, ms, defIdDesp, point, rotation, rotation, slot, 0.0, _layerNameDesp);
                    if (!newId.IsNull)
                    {
                        int hp = (hpPorDespliegue != null && dIdx < hpPorDespliegue.Length)
                            ? hpPorDespliegue[dIdx]
                            : 0;
                        if (odQueue != null) odQueue.Add(System.Tuple.Create(newId, hp));
                        dIdx++;
                    }
                    inserted++;
                    d--;
                    if (!esPar1D1C) slot++;
                }
                if (c > 0)
                {
                    double extraY = esPar1D1C ? GeometryConstants.CTO_CREC_OFFSET_ADICIONAL : 0.0;
                    ObjectId newId = InsertBlock(tr, ms, defIdCrec, point, rotation, rotation, slot, extraY, _layerNameCrec);
                    if (!newId.IsNull && odQueue != null)
                        odQueue.Add(System.Tuple.Create(newId, 0));
                    inserted++;
                    c--;
                    slot++;
                }
            }

            return inserted;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private ObjectId InsertBlock(Transaction tr, BlockTableRecord ms,
                                 ObjectId defId, Point3d polePoint,
                                 double displayAngle, double offsetAngle, int slot,
                                 double extraOffsetY, string layerName)
        {
            // offsetAngle controla hacia qué lado va el bloque (vereda correcta).
            // displayAngle controla la rotación visual (texto legible).
            Vector3d localOffset = new Vector3d(
                slot * GeometryConstants.CTO_SEPARACION,
                GeometryConstants.CTO_OFFSET_Y + extraOffsetY,
                0);
            Vector3d worldOffset = localOffset.RotateBy(offsetAngle, Vector3d.ZAxis);
            Point3d insPt = polePoint + worldOffset;

            using (var br = new BlockReference(insPt, defId))
            {
                br.Rotation = displayAngle;
                br.Layer    = layerName;
                ms.AppendEntity(br);
                tr.AddNewlyCreatedDBObject(br, true);
                return br.ObjectId;
            }
        }

        /// <summary>
        /// Retorna dos ángulos para el despliegue:
        /// - offsetAngle: paralelo al eje de calle, lado correcto (sin clampear). Usado para calcular worldOffset.
        /// - displayAngle: clampeado al rango legible [0°,90°]∪[270°,360°]. Usado para br.Rotation.
        /// Separar ambos evita que el clamping de legibilidad invierta el lado de la vereda.
        /// </summary>
        private static (double displayAngle, double offsetAngle) ComputeDeploymentAngles(
            Transaction tr, Database db, ObjectId poleId, Point3d polePoint)
        {
            string segHex   = XDataManager.GetString(tr, poleId, XDataKeys.ID_SEGMENT);
            string lingaHex = XDataManager.GetString(tr, poleId, XDataKeys.ID_LINGA);

            // Segmento primero (eje de calle, referencia canónica); linga como fallback.
            Curve refCurve = ResolveCurve(tr, db, segHex) ?? ResolveCurve(tr, db, lingaHex);
            if (refCurve == null) return (0.0, 0.0);

            // dir y cross del MISMO refCurve: consistente, sin mezcla de curvas.
            Vector3d dir = (refCurve.EndPoint - refCurve.StartPoint).GetNormal();
            double baseAngle = Math.Atan2(dir.Y, dir.X);

            Point3d closest = refCurve.GetClosestPointTo(polePoint, false);
            Vector3d toPole = polePoint - closest;
            double cross = dir.X * toPole.Y - dir.Y * toPole.X;

            // offsetAngle: paralelo al eje. Rotación local de (0, CTO_OFFSET_Y, 0) apunta
            // en la dirección perpendicular correcta hacia la vereda.
            // cross >= 0 → poste a la IZQUIERDA del sentido del segmento → sumar π.
            // cross < 0  → poste a la DERECHA → sin suma.
            double offsetAngle = baseAngle + (cross >= 0 ? Math.PI : 0.0);

            // displayAngle: clampeado para legibilidad. El clamping puede girar 180° el ángulo
            // visual, pero NO afecta offsetAngle, con lo que el offset sigue siendo correcto.
            const double TwoPi = 2.0 * Math.PI;
            double displayAngle = ((offsetAngle % TwoPi) + TwoPi) % TwoPi;
            if (displayAngle > Math.PI / 2.0 && displayAngle < 3.0 * Math.PI / 2.0)
                displayAngle += Math.PI;
            displayAngle = ((displayAngle % TwoPi) + TwoPi) % TwoPi;

            return (displayAngle, offsetAngle);
        }

        private static Curve ResolveCurve(Transaction tr, Database db, string handleHex)
        {
            if (string.IsNullOrEmpty(handleHex)) return null;
            if (!long.TryParse(handleHex, System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out long handleValue))
                return null;
            if (!db.TryGetObjectId(new Handle(handleValue), out ObjectId curveId)) return null;
            return tr.GetObject(curveId, OpenMode.ForRead) as Curve;
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
