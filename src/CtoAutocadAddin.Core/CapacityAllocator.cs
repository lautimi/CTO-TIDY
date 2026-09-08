using System;
using System.Collections.Generic;

namespace Koovra.Cto.Core
{
    /// <summary>
    /// Asigna propiedades servicio a cajas CTO respetando un cupo de HP por caja.
    /// Pura (sin dependencias a AutoCAD) y totalmente testeable.
    ///
    /// El recorrido es en orden paramétrico sobre el eje de calle — no por distancia —
    /// porque asignar por cercanía produce spiders cruzados entre propiedades vecinas.
    /// </summary>
    public static class CapacityAllocator
    {
        public const int DEFAULT_CAPACITY = 8;

        public class PropRef
        {
            public string Id;
            public int    Hp;
            public double X, Y;
            public string SegmentId;
            public double Param;   // posición proyectada sobre el eje del segmento
            public int    Side;    // +1 / -1 según vereda; 0 = indeterminado
        }

        public class BoxRef
        {
            public string Id;
            public double X, Y;
            public string SegmentId;
            public double Param;
            public int    Side;
        }

        public class AllocationResult
        {
            public Dictionary<string, string> BoxByProperty =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, int> LoadByBox =
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            /// <summary>Propiedades asignadas por encima del cupo de su caja.</summary>
            public List<string> OverCapacity = new List<string>();

            /// <summary>Propiedades cuyo segmento no tiene ninguna caja.</summary>
            public List<string> Unassigned = new List<string>();
        }

        public static AllocationResult Allocate(
            IList<PropRef> props, IList<BoxRef> boxes, int capacity = DEFAULT_CAPACITY)
        {
            var result = new AllocationResult();
            if (capacity <= 0) capacity = DEFAULT_CAPACITY;
            if (props == null || props.Count == 0) return result;

            var boxesBySegment = new Dictionary<string, List<BoxRef>>(StringComparer.OrdinalIgnoreCase);
            if (boxes != null)
            {
                foreach (BoxRef b in boxes)
                {
                    if (b == null || string.IsNullOrEmpty(b.Id)) continue;
                    result.LoadByBox[b.Id] = 0;
                    string seg = b.SegmentId ?? string.Empty;
                    List<BoxRef> list;
                    if (!boxesBySegment.TryGetValue(seg, out list))
                        boxesBySegment[seg] = list = new List<BoxRef>();
                    list.Add(b);
                }
            }

            var propsBySegment = new Dictionary<string, List<PropRef>>(StringComparer.OrdinalIgnoreCase);
            foreach (PropRef p in props)
            {
                if (p == null || string.IsNullOrEmpty(p.Id)) continue;
                string seg = p.SegmentId ?? string.Empty;
                List<PropRef> list;
                if (!propsBySegment.TryGetValue(seg, out list))
                    propsBySegment[seg] = list = new List<PropRef>();
                list.Add(p);
            }

            foreach (var kv in propsBySegment)
            {
                List<BoxRef> segBoxes;
                if (!boxesBySegment.TryGetValue(kv.Key, out segBoxes) || segBoxes.Count == 0)
                {
                    foreach (PropRef p in kv.Value) result.Unassigned.Add(p.Id);
                    continue;
                }
                AllocateSegment(kv.Value, segBoxes, capacity, result);
            }

            return result;
        }

        private static void AllocateSegment(
            List<PropRef> segProps, List<BoxRef> segBoxes, int capacity, AllocationResult result)
        {
            foreach (var sideGroup in GroupBySide(segProps))
            {
                // Preferimos las cajas de la misma vereda. Si esa vereda no tiene ninguna,
                // usamos todas las del segmento: no es exceso de cupo, es que no hay caja de ese lado.
                List<BoxRef> candidates = FilterBySide(segBoxes, sideGroup.Key);
                if (candidates.Count == 0) candidates = new List<BoxRef>(segBoxes);
                candidates.Sort(CompareBoxes);

                List<PropRef> ordered = new List<PropRef>(sideGroup.Value);
                ordered.Sort(CompareProps);

                int cursor = 0;
                foreach (PropRef p in ordered)
                {
                    int hp = p.Hp > 0 ? p.Hp : 1;
                    BoxRef target = null;

                    while (cursor < candidates.Count)
                    {
                        BoxRef b = candidates[cursor];
                        if (result.LoadByBox[b.Id] + hp <= capacity) { target = b; break; }
                        cursor++;
                    }

                    if (target == null)
                        target = FirstWithRoom(segBoxes, hp, capacity, result);

                    if (target == null)
                    {
                        // Cupo agotado en todo el segmento. Igual se dibuja el spider: nunca
                        // dejamos una propiedad sin línea, se marca para que se vea en el plano.
                        target = Nearest(segBoxes, p);
                        result.OverCapacity.Add(p.Id);
                    }

                    result.BoxByProperty[p.Id] = target.Id;
                    result.LoadByBox[target.Id] = result.LoadByBox[target.Id] + hp;
                }
            }
        }

        private static SortedDictionary<int, List<PropRef>> GroupBySide(List<PropRef> items)
        {
            var map = new SortedDictionary<int, List<PropRef>>();
            foreach (PropRef p in items)
            {
                List<PropRef> list;
                if (!map.TryGetValue(p.Side, out list)) map[p.Side] = list = new List<PropRef>();
                list.Add(p);
            }
            return map;
        }

        private static List<BoxRef> FilterBySide(List<BoxRef> boxes, int side)
        {
            var list = new List<BoxRef>();
            foreach (BoxRef b in boxes) if (b.Side == side) list.Add(b);
            return list;
        }

        private static BoxRef FirstWithRoom(
            List<BoxRef> boxes, int hp, int capacity, AllocationResult result)
        {
            var ordered = new List<BoxRef>(boxes);
            ordered.Sort(CompareBoxes);
            foreach (BoxRef b in ordered)
                if (result.LoadByBox[b.Id] + hp <= capacity) return b;
            return null;
        }

        private static BoxRef Nearest(List<BoxRef> boxes, PropRef p)
        {
            BoxRef best = null;
            double bestDist = double.MaxValue;
            foreach (BoxRef b in boxes)
            {
                double dx = b.X - p.X, dy = b.Y - p.Y;
                double d = dx * dx + dy * dy;
                if (best == null || d < bestDist ||
                    (d == bestDist && string.CompareOrdinal(b.Id, best.Id) < 0))
                {
                    bestDist = d;
                    best = b;
                }
            }
            return best;
        }

        private static int CompareProps(PropRef a, PropRef b)
        {
            int c = a.Param.CompareTo(b.Param);
            return c != 0 ? c : string.CompareOrdinal(a.Id, b.Id);
        }

        private static int CompareBoxes(BoxRef a, BoxRef b)
        {
            int c = a.Param.CompareTo(b.Param);
            return c != 0 ? c : string.CompareOrdinal(a.Id, b.Id);
        }
    }
}
