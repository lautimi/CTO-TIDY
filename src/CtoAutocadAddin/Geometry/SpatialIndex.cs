using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace Koovra.Cto.AutocadAddin.Geometry
{
    /// <summary>
    /// Índice espacial simple basado en bounding boxes precomputados.
    /// Reemplaza QgsSpatialIndex del script Python original.
    /// Para DWGs con &gt; 10k entidades, considerar sustituir por RBush.NET (cambio aislado).
    /// </summary>
    public class SpatialIndex
    {
        private readonly Dictionary<ObjectId, Extents3d> _extents;

        public SpatialIndex(Transaction tr, ObjectIdCollection ids)
        {
            _extents = new Dictionary<ObjectId, Extents3d>(ids.Count);
            foreach (ObjectId id in ids)
            {
                Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null) continue;
                try
                {
                    _extents[id] = ent.GeometricExtents;
                }
                catch
                {
                    // Entidades sin geometría válida se ignoran.
                }
            }
        }

        public IEnumerable<ObjectId> AllIds => _extents.Keys;

        public Extents3d GetExtents(ObjectId id) => _extents[id];

        /// <summary>
        /// Busca candidatos cuyo bbox intersecta el rango dado.
        /// </summary>
        public IEnumerable<ObjectId> QueryExtents(Extents3d query)
        {
            foreach (var kv in _extents)
            {
                if (kv.Value.Intersects(query)) yield return kv.Key;
            }
        }

        /// <summary>
        /// Top-K candidatos ordenados por distancia del centroide del bbox al punto.
        /// Retorna los IDs en orden creciente de distancia.
        /// </summary>
        public IEnumerable<ObjectId> NearestByCentroid(Point3d p, int k)
        {
            return _extents
                .OrderBy(kv => kv.Value.CenterDistanceTo(p))
                .Take(k)
                .Select(kv => kv.Key);
        }

        /// <summary>
        /// Encuentra la curva más cercana al punto usando GetClosestPointTo sobre los top-K candidatos.
        /// </summary>
        public bool TryFindClosest(Transaction tr, Point3d p,
            out ObjectId bestId, out Point3d closest, out double distance)
        {
            bestId = ObjectId.Null;
            closest = Point3d.Origin;
            distance = double.MaxValue;

            foreach (ObjectId id in NearestByCentroid(p, GeometryConstants.NEAREST_NEIGHBOR_K))
            {
                Curve curve = tr.GetObject(id, OpenMode.ForRead) as Curve;
                if (curve == null) continue;

                Point3d cp = curve.GetClosestPointTo(p, false);
                double d = cp.DistanceTo(p);
                if (d < distance)
                {
                    distance = d;
                    bestId = id;
                    closest = cp;
                }
            }

            return bestId != ObjectId.Null;
        }
    }
}
