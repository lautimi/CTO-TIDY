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

            // Lista de segmentos Line para la fase 2 (intersección geométrica).
            var segsWithLine = new List<(ObjectId id, Line line, string calleCanon)>();

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

                // Acumular solo Lines para la fase 2.
                var lineObj = curve as Line;
                if (lineObj != null)
                    segsWithLine.Add((kv.Key, lineObj, calle));
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

            int phase1Count = corners.Count;

            // Fase 2: intersección geométrica de líneas extendidas.
            int phase2Count = BuildPhase2_LineIntersections(segsWithLine, corners, seenKeys);

            // Indexar por calle (incluye corners de fase 1 y fase 2).
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

            AcadLogger.Info(
                $"StreetCornerLibrary: {segmentsConsidered} segs / {endpointsAdded} endpoints → " +
                $"{corners.Count} esquinas ({phase1Count} fase1, {phase2Count} fase2), " +
                $"{byStreet.Count} calles. (sinNombre={segmentsSinNombre}, noLine={segmentsNoLine})");
            return new StreetCornerLibrary(corners, byStreet);
        }

        private static int BuildPhase2_LineIntersections(
            List<(ObjectId id, Line line, string calleCanon)> segs,
            List<StreetCorner> corners,
            HashSet<string> added)
        {
            // Poblar el set de deduplicación con las esquinas ya existentes de fase 1.
            // Las claves de fase 1 usan un formato diferente al de fase 2, así que
            // usamos MakeDedupeKey para agregar las existentes antes de iterar.
            foreach (var ec in corners)
            {
                string existingKey = MakeDedupeKey(ec.CalleA, ec.CalleB, ec.Point);
                added.Add(existingKey);
            }

            int count = 0;
            int n = segs.Count;
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    var a = segs[i];
                    var b = segs[j];
                    if (a.calleCanon == b.calleCanon) continue;

                    Point3d ip;
                    if (!TryLineIntersection(a.line, b.line, out ip)) continue;

                    double dA = Math.Min(
                        (ip - a.line.StartPoint).Length,
                        (ip - a.line.EndPoint).Length);
                    double dB = Math.Min(
                        (ip - b.line.StartPoint).Length,
                        (ip - b.line.EndPoint).Length);
                    if (dA > GeometryConstants.MAX_INTERSECTION_DIST) continue;
                    if (dB > GeometryConstants.MAX_INTERSECTION_DIST) continue;

                    string key = MakeDedupeKey(a.calleCanon, b.calleCanon, ip);
                    if (!added.Add(key)) continue;

                    corners.Add(new StreetCorner
                    {
                        Point  = ip,
                        CalleA = a.calleCanon,
                        CalleB = b.calleCanon,
                    });
                    count++;
                }
            }
            return count;
        }

        private static bool TryLineIntersection(Line a, Line b, out Point3d ip)
        {
            ip = Point3d.Origin;
            Point3d p1 = a.StartPoint;
            Point3d p2 = a.EndPoint;
            Point3d p3 = b.StartPoint;
            Point3d p4 = b.EndPoint;

            double dx1 = p2.X - p1.X;
            double dy1 = p2.Y - p1.Y;
            double dx2 = p4.X - p3.X;
            double dy2 = p4.Y - p3.Y;

            double denom = dx1 * dy2 - dy1 * dx2;
            double len1 = Math.Sqrt(dx1 * dx1 + dy1 * dy1);
            double len2 = Math.Sqrt(dx2 * dx2 + dy2 * dy2);
            if (len1 < 1e-9 || len2 < 1e-9) return false;
            double sinTheta = Math.Abs(denom) / (len1 * len2);
            if (sinTheta < 0.05) return false; // < ~3°

            double t = ((p3.X - p1.X) * dy2 - (p3.Y - p1.Y) * dx2) / denom;
            double x = p1.X + t * dx1;
            double y = p1.Y + t * dy1;
            ip = new Point3d(x, y, 0);
            return true;
        }

        private static string MakeDedupeKey(string calleA, string calleB, Point3d p)
        {
            string a = string.CompareOrdinal(calleA, calleB) <= 0 ? calleA : calleB;
            string b = string.CompareOrdinal(calleA, calleB) <= 0 ? calleB : calleA;
            int rx = (int)Math.Round(p.X / 0.5);
            int ry = (int)Math.Round(p.Y / 0.5);
            return $"{a}|{b}|{rx}|{ry}";
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
