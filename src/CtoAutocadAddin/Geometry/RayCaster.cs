using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace Koovra.Cto.AutocadAddin.Geometry
{
    /// <summary>
    /// Lanza dos rayos ortogonales de longitud RAY_LENGTH desde un punto de origen
    /// y busca la intersección más cercana al punto de referencia (el poste) entre
    /// una colección de curvas candidatas. Equivalente a rayo1/rayo2 del script Python.
    /// </summary>
    public static class RayCaster
    {
        public class RayHit
        {
            public Point3d Point;
            public ObjectId SegmentObjectId;
            public double DistanceToReference;
        }

        /// <summary>
        /// Lanza dos rayos en direcciones +normal y −normal desde rayOrigin, intersecta contra
        /// cada curva en segmentIds, y devuelve el RayHit más cercano a referencePoint
        /// cuya distancia sea mayor que EPSILON_DIST.
        /// </summary>
        public static RayHit CastOrthogonalRays(
            Transaction tr,
            Point3d rayOrigin,
            Vector3d normal,
            ObjectIdCollection segmentIds,
            Point3d referencePoint)
        {
            RayHit best = null;
            double bestDist = double.MaxValue;

            Point3d endPos = rayOrigin + normal * GeometryConstants.RAY_LENGTH;
            Point3d endNeg = rayOrigin - normal * GeometryConstants.RAY_LENGTH;

            using (var ray1 = new Line(rayOrigin, endPos))
            using (var ray2 = new Line(rayOrigin, endNeg))
            {
                foreach (ObjectId segId in segmentIds)
                {
                    Curve curve = tr.GetObject(segId, OpenMode.ForRead) as Curve;
                    if (curve == null) continue;

                    UpdateBest(ray1, curve, segId, referencePoint, ref best, ref bestDist);
                    UpdateBest(ray2, curve, segId, referencePoint, ref best, ref bestDist);
                }
            }

            return best;
        }

        private static void UpdateBest(Line ray, Curve target, ObjectId targetId,
            Point3d reference, ref RayHit best, ref double bestDist)
        {
            using (var pts = new Point3dCollection())
            {
                try
                {
                    ray.IntersectWith(target, Intersect.OnBothOperands, pts, IntPtr.Zero, IntPtr.Zero);
                }
                catch
                {
                    return;
                }

                foreach (Point3d ip in pts)
                {
                    double d = ip.DistanceTo(reference);
                    if (d <= GeometryConstants.EPSILON_DIST) continue;
                    if (d >= bestDist) continue;

                    bestDist = d;
                    best = new RayHit
                    {
                        Point = ip,
                        SegmentObjectId = targetId,
                        DistanceToReference = d,
                    };
                }
            }
        }
    }
}
