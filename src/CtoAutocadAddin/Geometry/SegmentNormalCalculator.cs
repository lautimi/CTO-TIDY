using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace Koovra.Cto.AutocadAddin.Geometry
{
    /// <summary>
    /// Calcula la normal del segmento tangente de una Polyline en el punto dado.
    /// Equivalente a closestSegmentWithContext + cálculo nx=-dy/L, ny=dx/L del script Python.
    /// </summary>
    public static class SegmentNormalCalculator
    {
        /// <summary>
        /// Devuelve el vector normal unitario (rotado 90° respecto a la tangente) del segmento
        /// de la polilínea que contiene el punto dado.
        /// </summary>
        public static Vector3d ComputeNormalAt(Polyline pl, Point3d pointOnPolyline)
        {
            if (pl == null) throw new ArgumentNullException(nameof(pl));

            int segIdx = FindSegmentIndexContaining(pl, pointOnPolyline);
            LineSegment3d seg = pl.GetLineSegmentAt(segIdx);

            Vector3d tangent = seg.EndPoint - seg.StartPoint;
            double length = tangent.Length;
            if (length < Tolerance.Global.EqualPoint)
            {
                return new Vector3d(1, 0, 0);
            }

            tangent = tangent / length;
            return new Vector3d(-tangent.Y, tangent.X, 0);
        }

        /// <summary>
        /// Encuentra el índice del segmento de la polilínea más cercano al punto.
        /// Polyline.NumberOfVertices devuelve N; segmentos válidos: 0..N-2 (o 0..N-1 si es cerrada).
        /// </summary>
        public static int FindSegmentIndexContaining(Polyline pl, Point3d point)
        {
            int segCount = pl.Closed ? pl.NumberOfVertices : pl.NumberOfVertices - 1;
            if (segCount <= 0) return 0;

            int best = 0;
            double bestDist = double.MaxValue;

            for (int i = 0; i < segCount; i++)
            {
                LineSegment3d seg = pl.GetLineSegmentAt(i);
                Point3d closest = seg.GetClosestPointTo(point).Point;
                double d = closest.DistanceTo(point);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = i;
                }
            }

            return best;
        }
    }
}
