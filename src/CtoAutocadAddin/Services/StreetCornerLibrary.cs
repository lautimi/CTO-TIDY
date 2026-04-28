using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Koovra.Cto.AutocadAddin.Geometry;
using Koovra.Cto.AutocadAddin.Infrastructure;
using Koovra.Cto.AutocadAddin.Map;

namespace Koovra.Cto.AutocadAddin.Services
{
    /// <summary>
    /// Una esquina de calle: punto donde se tocan los endpoints de dos Lines de
    /// segmentos con CALLE_1 distintos. Es la verdadera definición topológica
    /// de "esquina urbana" — invariante a ochavas, curvas, ángulos de corte.
    /// </summary>
    public class StreetCorner
    {
        public Point3d Point;
        public string  CalleA;   // canonizada: trim+upper
        public string  CalleB;   // distinta de CalleA (también canonizada)

        public bool InvolvesStreet(string canon)
        {
            return CalleA == canon || CalleB == canon;
        }

        public string OtherStreet(string canon)
        {
            return CalleA == canon ? CalleB : CalleA;
        }
    }

    /// <summary>
    /// Biblioteca de esquinas de calle construida UNA VEZ por DWG durante
    /// el procesamiento de postes. Indexa por hash espacial para lookups O(1)
    /// y por nombre de calle para queries del tipo "esquina más cercana de
    /// la calle X al punto P".
    ///
    /// Algoritmo de Build:
    /// 1) Para cada segmento (Line) con CALLE_1 conocida y distinta de CALLE_SIN_NOMBRE:
    ///    - Tomar Start y End.
    ///    - Insertar en hash espacial { celda → List<(point, calleName)> }.
    /// 2) Recorrer cada celda y sus 8 vecinas:
    ///    - Buscar pares (a, b) con calleA != calleB y dist(pa, pb) <= TOLERANCE.
    ///    - Crear StreetCorner deduplicado por (cellKey, sorted(calleA, calleB)).
    /// 3) Indexar las esquinas resultantes por nombre canonizado de calle.
    ///
    /// Performance: O(N) en construcción, O(K) en query donde K = esquinas por calle.
    /// </summary>
    public class StreetCornerLibrary
    {
        private readonly List<StreetCorner> _all;
        private readonly Dictionary<string, List<StreetCorner>> _byStreet;

        public int CornerCount => _all.Count;
        public int StreetCount => _byStreet.Count;
        public IReadOnlyList<StreetCorner> All => _all;

        private StreetCornerLibrary(List<StreetCorner> all, Dictionary<string, List<StreetCorner>> byStreet)
        {
            _all = all;
            _byStreet = byStreet;
        }

        /// <summary>
        /// Construye la biblioteca a partir de un dict ObjectId→nombre de calle.
        /// Lee los Line endpoints dentro de la transacción provista.
        /// Segmentos con nombre "CALLE SIN NOMBRE" o vacío se omiten.
        /// </summary>
        public static StreetCornerLibrary Build(Transaction tr, Dictionary<ObjectId, string> calleByOid)
        {
            double tol = GeometryConstants.STREET_CORNER_TOLERANCE;

            // Hash espacial: clave = (round(x/tol), round(y/tol))
            // Cada celda guarda los endpoints que cayeron en ella.
            var grid = new Dictionary<(int, int), List<(Point3d pt, string calleCanon)>>();

            int segmentsConsidered = 0;
            int endpointsAdded = 0;
            int segmentsSinNombre = 0;
            int segmentsNoLine = 0;

            foreach (var kv in calleByOid)
            {
                string calle = Canon(kv.Value);
                if (string.IsNullOrEmpty(calle) || calle == Canon(ObjectDataReader.CALLE_SIN_NOMBRE))
                {
                    segmentsSinNombre++;
                    continue;
                }

                Curve curve;
                try { curve = tr.GetObject(kv.Key, OpenMode.ForRead) as Curve; }
                catch { continue; }
                if (curve == null) { segmentsNoLine++; continue; }

                segmentsConsidered++;

                AddEndpoint(grid, curve.StartPoint, calle, tol);
                AddEndpoint(grid, curve.EndPoint, calle, tol);
                endpointsAdded += 2;
            }

            // Detectar esquinas: por cada celda, comparar todos los endpoints contra
            // los de la celda y los de las 8 vecinas (para evitar perder pares cerca del borde).
            var corners = new List<StreetCorner>();
            var seenKeys = new HashSet<string>();

            foreach (var cell in grid.Keys)
            {
                var here = grid[cell];
                for (int dx = 0; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        // Para evitar duplicar comparaciones entre pares de celdas, solo
                        // miramos las celdas (dx, dy) >= (0, *) en orden lexicográfico
                        // pero cada par (a, b) puede aparecer en una sola dirección.
                        // Simpler: pasamos por todos los offsets (-1..1, -1..1) y dedupe
                        // por seenKeys al final.
                        if (dx == 0 && dy < 0) continue; // evita duplicar (0,-1) vs (0,1) misma celda
                        var nbrKey = (cell.Item1 + dx, cell.Item2 + dy);
                        if (!grid.TryGetValue(nbrKey, out var there)) continue;

                        for (int i = 0; i < here.Count; i++)
                        {
                            var (pa, ca) = here[i];
                            int jStart = (dx == 0 && dy == 0) ? i + 1 : 0;
                            for (int j = jStart; j < there.Count; j++)
                            {
                                var (pb, cb) = there[j];
                                if (ca == cb) continue;
                                if ((pa - pb).Length > tol) continue;

                                // Deduplicar por celda + par-de-nombres
                                string nameKey = string.Compare(ca, cb, StringComparison.Ordinal) <= 0
                                    ? ca + "||" + cb : cb + "||" + ca;
                                int cx = (int)Math.Round((pa.X + pb.X) / 2 / tol);
                                int cy = (int)Math.Round((pa.Y + pb.Y) / 2 / tol);
                                string dedupKey = $"{cx},{cy}|{nameKey}";
                                if (!seenKeys.Add(dedupKey)) continue;

                                corners.Add(new StreetCorner
                                {
                                    Point = new Point3d(
                                        (pa.X + pb.X) * 0.5,
                                        (pa.Y + pb.Y) * 0.5,
                                        (pa.Z + pb.Z) * 0.5),
                                    CalleA = ca,
                                    CalleB = cb,
                                });
                            }
                        }
                    }
                }
            }

            // Indexar por calle
            var byStreet = new Dictionary<string, List<StreetCorner>>();
            foreach (var c in corners)
            {
                if (!byStreet.TryGetValue(c.CalleA, out var listA))
                {
                    listA = new List<StreetCorner>();
                    byStreet[c.CalleA] = listA;
                }
                listA.Add(c);

                if (!byStreet.TryGetValue(c.CalleB, out var listB))
                {
                    listB = new List<StreetCorner>();
                    byStreet[c.CalleB] = listB;
                }
                listB.Add(c);
            }

            AcadLogger.Info($"StreetCornerLibrary: {segmentsConsidered} segs / {endpointsAdded} endpoints → {corners.Count} esquinas, {byStreet.Count} calles. (sinNombre={segmentsSinNombre}, noLine={segmentsNoLine})");
            return new StreetCornerLibrary(corners, byStreet);
        }

        /// <summary>
        /// Devuelve la esquina más cercana a <paramref name="query"/> que involucra
        /// la calle <paramref name="calleName"/>, dentro de <paramref name="maxDist"/>.
        /// null si no hay ninguna.
        /// </summary>
        public StreetCorner FindNearestForStreet(Point3d query, string calleName, double maxDist)
        {
            string canon = Canon(calleName);
            if (string.IsNullOrEmpty(canon)) return null;
            if (!_byStreet.TryGetValue(canon, out var list)) return null;

            StreetCorner best = null;
            double bestD = double.MaxValue;
            foreach (var c in list)
            {
                double d = (c.Point - query).Length;
                if (d < bestD)
                {
                    bestD = d;
                    best = c;
                }
            }
            if (bestD > maxDist) return null;
            return best;
        }

        // --- helpers ---

        public static string Canon(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            return s.Trim().ToUpperInvariant();
        }

        private static void AddEndpoint(Dictionary<(int, int), List<(Point3d, string)>> grid,
                                        Point3d pt, string calleCanon, double tol)
        {
            int cx = (int)Math.Round(pt.X / tol);
            int cy = (int)Math.Round(pt.Y / tol);
            var key = (cx, cy);
            if (!grid.TryGetValue(key, out var list))
            {
                list = new List<(Point3d, string)>();
                grid[key] = list;
            }
            list.Add((pt, calleCanon));
        }
    }
}
