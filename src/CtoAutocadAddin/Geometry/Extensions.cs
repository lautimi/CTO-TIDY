using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace Koovra.Cto.AutocadAddin.Geometry
{
    public static class Extensions
    {
        /// <summary>
        /// Devuelve el punto de inserción del poste: Position para DBPoint, Position para BlockReference,
        /// centroide del bbox como fallback para cualquier Entity.
        /// </summary>
        public static Point3d GetInsertionOrPosition(Entity ent)
        {
            if (ent == null) throw new ArgumentNullException(nameof(ent));

            switch (ent)
            {
                case BlockReference br: return br.Position;
                case DBPoint pt: return pt.Position;
                case DBText txt: return txt.Position;
                case MText mtxt: return mtxt.Location;
                case Circle c: return c.Center;
            }

            Extents3d ext = ent.GeometricExtents;
            return new Point3d(
                (ext.MinPoint.X + ext.MaxPoint.X) * 0.5,
                (ext.MinPoint.Y + ext.MaxPoint.Y) * 0.5,
                (ext.MinPoint.Z + ext.MaxPoint.Z) * 0.5);
        }

        public static bool Intersects(this Extents3d a, Extents3d b)
        {
            return a.MinPoint.X <= b.MaxPoint.X && a.MaxPoint.X >= b.MinPoint.X
                && a.MinPoint.Y <= b.MaxPoint.Y && a.MaxPoint.Y >= b.MinPoint.Y;
        }

        public static double CenterDistanceTo(this Extents3d ext, Point3d p)
        {
            double cx = (ext.MinPoint.X + ext.MaxPoint.X) * 0.5;
            double cy = (ext.MinPoint.Y + ext.MaxPoint.Y) * 0.5;
            double dx = cx - p.X;
            double dy = cy - p.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public static Extents3d Inflate(this Extents3d ext, double r)
        {
            return new Extents3d(
                new Point3d(ext.MinPoint.X - r, ext.MinPoint.Y - r, ext.MinPoint.Z - r),
                new Point3d(ext.MaxPoint.X + r, ext.MaxPoint.Y + r, ext.MaxPoint.Z + r));
        }
    }
}
