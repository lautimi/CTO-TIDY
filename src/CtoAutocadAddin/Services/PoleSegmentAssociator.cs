using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Koovra.Cto.AutocadAddin.Geometry;
using Koovra.Cto.Core;

namespace Koovra.Cto.AutocadAddin.Services
{
    /// <summary>
    /// Orquesta el paso 1 (asociación poste → segmento) replicando el script
    /// "Postes segmento union final.py" sobre la API de AutoCAD.
    /// </summary>
    public class PoleSegmentAssociator
    {
        public class Outcome
        {
            public string SegmentId;
            public ObjectId SegmentObjectId;
            public string Estado;
            public double SegmentLength;      // largo en metros — usado en CTO_CALCULAR
            public Point3d? PointOnSegment;
            public Point3d? PointOnManzana;
            public ObjectId ManzanaObjectId;  // ObjectId.Null si no se asoció a ninguna manzana
        }

        private readonly SpatialIndex _manzanasIndex;
        private readonly ObjectIdCollection _segmentosIds;

        public PoleSegmentAssociator(SpatialIndex manzanasIndex, ObjectIdCollection segmentosIds)
        {
            _manzanasIndex = manzanasIndex;
            _segmentosIds = segmentosIds;
        }

        public Outcome AssociatePole(Transaction tr, ObjectId poleId)
        {
            Entity poleEnt = tr.GetObject(poleId, OpenMode.ForRead) as Entity;
            if (poleEnt == null) return new Outcome { Estado = AddressMatcher.SIN_SEGMENTO };

            Point3d polePt = Extensions.GetInsertionOrPosition(poleEnt);

            if (!_manzanasIndex.TryFindClosest(tr, polePt,
                    out ObjectId bestManzanaId, out Point3d closestOnManzana, out double distToManzana))
            {
                return new Outcome { Estado = AddressMatcher.SIN_SEGMENTO };
            }

            Polyline manzana = tr.GetObject(bestManzanaId, OpenMode.ForRead) as Polyline;
            if (manzana == null) return new Outcome { Estado = AddressMatcher.SIN_SEGMENTO };

            Vector3d normal = SegmentNormalCalculator.ComputeNormalAt(manzana, closestOnManzana);

            RayCaster.RayHit hit = RayCaster.CastOrthogonalRays(tr, closestOnManzana, normal, _segmentosIds, polePt);
            if (hit == null) return new Outcome { Estado = AddressMatcher.SIN_SEGMENTO };

            double tolerance = distToManzana + GeometryConstants.ANTI_CROSS_MARGIN;
            if (!AntiCrossingFilter.IsValid(tr, polePt, hit.Point, _manzanasIndex, tolerance))
            {
                return new Outcome { Estado = AddressMatcher.SIN_SEGMENTO };
            }

            // Largo del segmento (Line.Length) — necesario para la tabla CTO (≤160m vs >160m).
            double segLength = 0.0;
            Curve segCurve = tr.GetObject(hit.SegmentObjectId, OpenMode.ForRead) as Curve;
            if (segCurve != null)
                segLength = (segCurve.EndPoint - segCurve.StartPoint).Length;

            // Usar el Handle hex como ID único del segmento en este DWG.
            // CtoBlockDeployer lo parsea de vuelta a ObjectId con db.TryGetObjectId(handle).
            string segId = hit.SegmentObjectId.Handle.ToString();

            return new Outcome
            {
                SegmentId = segId,
                SegmentObjectId = hit.SegmentObjectId,
                Estado = AddressMatcher.OK,
                SegmentLength = segLength,
                PointOnSegment = hit.Point,
                PointOnManzana = closestOnManzana,
                ManzanaObjectId = bestManzanaId,
            };
        }
    }
}
