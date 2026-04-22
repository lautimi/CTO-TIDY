using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace Koovra.Cto.AutocadAddin.Geometry
{
    /// <summary>
    /// Valida que la línea poste → punto-sobre-segmento no atraviese otra manzana
    /// a distancia mayor que la tolerancia (distancia poste → manzana origen + 2 m).
    /// Replica el "filtro anti-cruce" del script Python.
    /// </summary>
    public static class AntiCrossingFilter
    {
        /// <summary>
        /// Devuelve true si la línea es válida (no cruza otra manzana indebidamente),
        /// false si debe descartarse el punto candidato.
        /// </summary>
        public static bool IsValid(
            Transaction tr,
            Point3d polePoint,
            Point3d segmentPoint,
            SpatialIndex manzanasIndex,
            double tolerance)
        {
            using (var testLine = new Line(polePoint, segmentPoint))
            {
                Extents3d lineExt = testLine.GeometricExtents;

                foreach (ObjectId id in manzanasIndex.AllIds)
                {
                    Extents3d mzExt = manzanasIndex.GetExtents(id);
                    if (!lineExt.Intersects(mzExt)) continue;

                    Curve curve = tr.GetObject(id, OpenMode.ForRead) as Curve;
                    if (curve == null) continue;

                    using (var pts = new Point3dCollection())
                    {
                        try
                        {
                            testLine.IntersectWith(curve, Intersect.OnBothOperands, pts, IntPtr.Zero, IntPtr.Zero);
                        }
                        catch
                        {
                            continue;
                        }

                        foreach (Point3d ip in pts)
                        {
                            double d = ip.DistanceTo(polePoint);
                            if (d > tolerance) return false;
                        }
                    }
                }
            }

            return true;
        }
    }
}
