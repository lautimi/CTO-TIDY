using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Koovra.Cto.AutocadAddin.Geometry;
using Koovra.Cto.AutocadAddin.Infrastructure;
using Koovra.Cto.AutocadAddin.Map;

namespace Koovra.Cto.AutocadAddin.Services
{
    public enum FrenteMethod
    {
        V4_StreetCorners,  // esquinas de calle nombradas (máxima precisión)
        V3_Projection,     // proyección de endpoints del segmento sobre la manzana
        V2_DetectCorners,  // detección angular de esquinas (fallback legacy)
        NotFound,          // no se pudo calcular
    }

    /// <summary>
    /// Identifica el FRENTE DE MANZANA al que pertenece un punto sobre el borde del polígono
    /// y calcula su largo real.
    ///
    /// Una manzana (LWPOLYLINE cerrada) tiene N frentes = N esquinas. Las esquinas se detectan
    /// como vértices donde la polilínea dobla > <see cref="CORNER_ANGLE_THRESHOLD_DEG"/> grados.
    /// Los vértices con cambio angular menor son "puntos intermedios" de dibujo y se absorben
    /// dentro del mismo frente.
    ///
    /// Cuando se pasa <c>segmentCurve</c> (la curva del segmento de calle asociado al poste),
    /// el largo se calcula proyectando los dos vértices-esquina del frente sobre esa curva y
    /// midiendo la distancia entre ambas proyecciones a lo largo de la curva. Este enfoque evita
    /// acumular edges intermedios de dibujo que distorsionan la medición.
    /// Sin curva (o con <c>null</c>), se usa el fallback: sumatoria de edges entre esquinas.
    /// </summary>
    public static class FrenteManzanaCalculator
    {
        /// <summary>
        /// Umbral en grados: un vértice de la polilínea se considera ESQUINA si el cambio de
        /// dirección entre el segmento entrante y el saliente supera este valor.
        /// 45° es estándar para manzanas urbanas.
        /// </summary>
        public const double CORNER_ANGLE_THRESHOLD_DEG = 45.0;

        public class Outcome
        {
            public int    FrenteIndex;      // 0..N-1 (orden de aparición dentro de la manzana)
            public int    StartCornerIdx;   // índice del vértice-esquina inicial
            public int    EndCornerIdx;     // índice del vértice-esquina final
            public double Largo;            // sumatoria de largos de segmentos entre ambas esquinas
            public bool   Found;            // false si la manzana no es válida o no pudo resolver
            public Point3d? CornerA;        // vértice-esquina inicial en la manzana (startCorner)
            public Point3d? CornerB;        // vértice-esquina final en la manzana (endCorner)
            public Point3d? ProjA;          // proyección de CornerA sobre el segmento de calle
            public Point3d? ProjB;          // proyección de CornerB sobre el segmento de calle
        }

        /// <summary>
        /// Devuelve el frente al que pertenece <paramref name="pointOnEdge"/>.
        /// El punto debería estar sobre el borde (típicamente es el
        /// <c>PointOnManzana</c> devuelto por el raycast del PoleSegmentAssociator).
        /// Usa el fallback de suma de edges (sin curva de segmento).
        /// </summary>
        public static Outcome ComputeFrente(Polyline manzana, Point3d pointOnEdge)
        {
            return ComputeFrente(manzana, pointOnEdge, segmentCurve: null);
        }

        /// <summary>
        /// Devuelve el frente al que pertenece <paramref name="pointOnEdge"/>.
        /// Cuando <paramref name="segmentCurve"/> no es null, el largo se obtiene proyectando
        /// los dos vértices-esquina sobre la curva del segmento de calle y midiendo la distancia
        /// entre proyecciones a lo largo de esa curva. Si es null, se usa la sumatoria de edges.
        /// </summary>
        public static Outcome ComputeFrente(Polyline manzana, Point3d pointOnEdge, Curve segmentCurve)
        {
            var empty = new Outcome { Found = false };
            if (manzana == null) return empty;
            if (!manzana.Closed) return empty;

            int nVerts = manzana.NumberOfVertices;
            if (nVerts < 3) return empty;

            // 1. Detectar esquinas
            List<int> corners = DetectCorners(manzana);
            if (corners.Count < 2)
            {
                // Sin esquinas detectables (polilínea muy suave / curvada) → un sólo frente,
                // el largo es el perímetro completo.
                return new Outcome
                {
                    Found          = true,
                    FrenteIndex    = 0,
                    StartCornerIdx = 0,
                    EndCornerIdx   = 0,
                    Largo          = manzana.Length,
                };
            }

            // 2. Segmento donde cayó el punto
            int segIdx = SegmentNormalCalculator.FindSegmentIndexContaining(manzana, pointOnEdge);

            // 3. Expandir hacia atrás y adelante hasta tocar esquinas.
            //    Convención: el "frente" se define por dos vértices-esquina consecutivos (A, B).
            //    Un segmento j (entre vértices j y j+1) pertenece al frente [A, B) si A <= j < B
            //    (en orden circular). StartCorner = A, EndCorner = B.
            int startCorner = FindPrevCorner(corners, segIdx, nVerts);
            int endCorner   = FindNextCorner(corners, segIdx, nVerts);

            // 4. Largo: proyección sobre curva del segmento, o suma de edges como fallback.
            double largo;
            Point3d cornerA = manzana.GetPoint3dAt(startCorner);
            Point3d cornerB = manzana.GetPoint3dAt(endCorner);
            Point3d? projA  = null;
            Point3d? projB  = null;

            if (segmentCurve != null)
            {
                Point3d pA = segmentCurve.GetClosestPointTo(cornerA, false);
                Point3d pB = segmentCurve.GetClosestPointTo(cornerB, false);
                double  dA = segmentCurve.GetDistAtPoint(pA);
                double  dB = segmentCurve.GetDistAtPoint(pB);
                largo  = Math.Abs(dB - dA);
                projA  = pA;
                projB  = pB;
            }
            else
            {
                // Fallback: algoritmo viejo (suma de edges entre esquinas)
                largo = 0.0;
                int j = startCorner;
                int safety = 0;
                while (j != endCorner && safety++ < nVerts + 1)
                {
                    LineSegment3d s = manzana.GetLineSegmentAt(j);
                    largo += (s.EndPoint - s.StartPoint).Length;
                    j = (j + 1) % nVerts;
                }
            }

            int frenteIdx = corners.IndexOf(startCorner);
            if (frenteIdx < 0) frenteIdx = 0;

            return new Outcome
            {
                Found          = true,
                FrenteIndex    = frenteIdx,
                StartCornerIdx = startCorner,
                EndCornerIdx   = endCorner,
                Largo          = largo,
                CornerA        = cornerA,
                CornerB        = cornerB,
                ProjA          = projA,
                ProjB          = projB,
            };
        }

        /// <summary>
        /// Overload V4: calcula LARGO_FRENTE usando la biblioteca de esquinas de calle.
        /// Cadena de fallback: V4 → V3_Projection → V2_DetectCorners.
        ///
        /// V4: toma los dos endpoints del segmento de calle, busca la esquina de calle
        /// más cercana a cada uno (involucrando la misma calle), proyecta esas esquinas
        /// sobre la manzana, y mide el arco que contiene <paramref name="pointOnEdge"/>.
        ///
        /// V3_Projection: si V4 falla (sin library, sin calleSegmento, o esquinas no encontradas),
        /// proyecta directamente los endpoints del segmento sobre la manzana y mide el arco.
        ///
        /// V2_DetectCorners: si segmentCurve es null, cae al overload con DetectCorners.
        /// </summary>
        public static Outcome ComputeFrente(
            Polyline manzana,
            Point3d  pointOnEdge,
            Curve    segmentCurve,
            StreetCornerLibrary corners,
            string   calleSegmento,
            out FrenteMethod method)
        {
            method = FrenteMethod.NotFound;
            var empty = new Outcome { Found = false };
            if (manzana == null) return empty;
            if (!manzana.Closed) return empty;
            if (manzana.NumberOfVertices < 3) return empty;

            // ── V4: esquinas de calle nombradas ───────────────────────────────────────
            if (corners != null && !string.IsNullOrEmpty(calleSegmento)
                && calleSegmento != ObjectDataReader.CALLE_SIN_NOMBRE
                && segmentCurve != null)
            {
                string canon = StreetCornerLibrary.Canon(calleSegmento);
                StreetCorner csStart = corners.FindNearestForStreet(
                    segmentCurve.StartPoint, canon, GeometryConstants.STREET_CORNER_SEARCH_MAX);
                StreetCorner csEnd   = corners.FindNearestForStreet(
                    segmentCurve.EndPoint,   canon, GeometryConstants.STREET_CORNER_SEARCH_MAX);

                if (csStart == null || csEnd == null)
                {
                    AcadLogger.Info($"V4 fallback: no se encontraron esquinas para calle '{canon}' " +
                        $"(csStart={(csStart == null ? "null" : "ok")} csEnd={(csEnd == null ? "null" : "ok")}) " +
                        $"cerca de S={segmentCurve.StartPoint.X:F1},{segmentCurve.StartPoint.Y:F1} " +
                        $"E={segmentCurve.EndPoint.X:F1},{segmentCurve.EndPoint.Y:F1}");
                    goto TryV3;
                }
                {
                    // Proyectar las esquinas de calle sobre la manzana
                    Point3d pS, pE, pP;
                    try
                    {
                        pS = manzana.GetClosestPointTo(csStart.Point, false);
                        pE = manzana.GetClosestPointTo(csEnd.Point,   false);
                        pP = manzana.GetClosestPointTo(pointOnEdge,   false);
                    }
                    catch { goto TryV3; }

                    // Validar que las esquinas no están demasiado lejos de la manzana
                    double distS = (pS - csStart.Point).Length;
                    double distE = (pE - csEnd.Point).Length;
                    if (distS > GeometryConstants.CORNER_TO_MANZANA_MAX
                     || distE > GeometryConstants.CORNER_TO_MANZANA_MAX)
                    {
                        AcadLogger.Info($"V4 fallback: esquina muy lejos de manzana (distS={distS:F2} distE={distE:F2} max={GeometryConstants.CORNER_TO_MANZANA_MAX})");
                        goto TryV3;
                    }

                    double largo = ComputeArc(manzana, pS, pE, pP);
                    if (largo > 0)
                    {
                        method = FrenteMethod.V4_StreetCorners;
                        return new Outcome
                        {
                            Found          = true,
                            FrenteIndex    = 0,
                            StartCornerIdx = -1,
                            EndCornerIdx   = -1,
                            Largo          = largo,
                            CornerA        = pS,            // proyección de csStart sobre manzana
                            CornerB        = pE,            // proyección de csEnd sobre manzana
                            ProjA          = csStart.Point, // esquina de calle real (Start)
                            ProjB          = csEnd.Point,   // esquina de calle real (End)
                        };
                    }
                }
            }

            TryV3:
            // ── V3: proyección directa de endpoints del segmento sobre la manzana ────
            if (segmentCurve != null)
            {
                Point3d S = segmentCurve.StartPoint;
                Point3d E = segmentCurve.EndPoint;

                Point3d pS, pE, pP;
                try
                {
                    pS = manzana.GetClosestPointTo(S, false);
                    pE = manzana.GetClosestPointTo(E, false);
                    pP = manzana.GetClosestPointTo(pointOnEdge, false);
                }
                catch { goto TryV2; }

                if ((pE - pS).Length < 1e-6)
                {
                    AcadLogger.Info($"V3 fallback: pS==pE (ambos endpoints del segmento proyectan al mismo punto en la manzana)");
                    goto TryV2;
                }

                double largo = ComputeArc(manzana, pS, pE, pP);
                if (largo > 0)
                {
                    method = FrenteMethod.V3_Projection;
                    return new Outcome
                    {
                        Found          = true,
                        FrenteIndex    = 0,
                        StartCornerIdx = -1,
                        EndCornerIdx   = -1,
                        Largo          = largo,
                        CornerA        = pS,
                        CornerB        = pE,
                        ProjA          = S,
                        ProjB          = E,
                    };
                }
            }

            TryV2:
            // ── V2: DetectCorners legacy ──────────────────────────────────────────────
            {
                var v2 = ComputeFrente(manzana, pointOnEdge, segmentCurve);
                if (v2.Found)
                {
                    method = FrenteMethod.V2_DetectCorners;
                    return v2;
                }
            }

            return empty;
        }

        /// <summary>
        /// Calcula el largo del arco de la polilínea cerrada entre pS y pE que
        /// contiene la proyección del poste (pP). Si pP no cae claramente en ninguno
        /// de los dos arcos (ruido numérico), elige el más corto.
        /// Devuelve 0 si no se puede calcular.
        /// </summary>
        private static double ComputeArc(Polyline manzana, Point3d pS, Point3d pE, Point3d pP)
        {
            double dS = SafeGetDistAtPoint(manzana, pS);
            double dE = SafeGetDistAtPoint(manzana, pE);
            double dP = SafeGetDistAtPoint(manzana, pP);
            if (dS < 0 || dE < 0 || dP < 0) return 0;

            double perimetro = manzana.Length;
            double lo = Math.Min(dS, dE);
            double hi = Math.Max(dS, dE);
            double directArc = hi - lo;
            double otherArc  = perimetro - directArc;

            bool dPInDirect = dP >= lo - 1e-6 && dP <= hi + 1e-6;
            double largo = dPInDirect ? directArc : otherArc;

            // Defensa contra ruido numérico
            if (largo <= 0 || largo > perimetro)
                largo = Math.Min(directArc, otherArc);

            if (largo <= 0) return 0;
            return largo;
        }

        /// <summary>
        /// Obtiene la distancia acumulada a lo largo de la polyline al punto dado.
        /// Más robusto que GetDistAtPoint directo: si el punto no está exactamente sobre
        /// la polyline (error de precisión flotante), re-proyecta antes de medir.
        /// Devuelve -1 si no se puede calcular.
        /// </summary>
        private static double SafeGetDistAtPoint(Polyline pl, Point3d pt)
        {
            // Intento 1: el punto ya debería estar sobre la polyline.
            try { return pl.GetDistAtPoint(pt); }
            catch { }

            // Intento 2: re-proyectar sobre la polyline para eliminar error flotante.
            try
            {
                Point3d snapped = pl.GetClosestPointTo(pt, false);
                return pl.GetDistAtPoint(snapped);
            }
            catch { }

            // Intento 3: recorrer segmentos manualmente y encontrar el más cercano.
            try
            {
                int n = pl.NumberOfVertices;
                double bestDist  = double.MaxValue;
                double accumDist = 0.0;
                double bestAccum = 0.0;

                for (int i = 0; i < n; i++)
                {
                    LineSegment3d seg = pl.GetLineSegmentAt(i);
                    Vector3d v   = seg.EndPoint - seg.StartPoint;
                    double   len = v.Length;
                    if (len < 1e-12) { accumDist += len; continue; }

                    // Proyección paramétrica del punto sobre el segmento
                    double t = (pt - seg.StartPoint).DotProduct(v) / (len * len);
                    t = Math.Max(0.0, Math.Min(1.0, t));
                    Point3d proj = seg.StartPoint + v * t;
                    double  d    = (pt - proj).Length;

                    if (d < bestDist)
                    {
                        bestDist  = d;
                        bestAccum = accumDist + t * len;
                    }
                    accumDist += len;
                }

                if (bestDist < double.MaxValue) return bestAccum;
            }
            catch { }

            return -1;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Lista ordenada de índices de vértices que son esquinas reales
        /// (cambio de dirección > umbral).
        /// </summary>
        private static List<int> DetectCorners(Polyline pl)
        {
            int n = pl.NumberOfVertices;
            var corners = new List<int>();
            double thresholdRad = CORNER_ANGLE_THRESHOLD_DEG * Math.PI / 180.0;

            for (int i = 0; i < n; i++)
            {
                // Segmentos incidentes: (i-1 → i) entrante, (i → i+1) saliente.
                int prev = (i - 1 + n) % n;
                int next = i;  // el segmento i parte del vértice i

                LineSegment3d segIn  = pl.GetLineSegmentAt(prev);
                LineSegment3d segOut = pl.GetLineSegmentAt(next);

                Vector3d vIn  = segIn.EndPoint  - segIn.StartPoint;
                Vector3d vOut = segOut.EndPoint - segOut.StartPoint;

                double liIn  = vIn.Length;
                double liOut = vOut.Length;
                if (liIn < 1e-6 || liOut < 1e-6) continue;

                vIn  = vIn  / liIn;
                vOut = vOut / liOut;

                // Ángulo entre vIn y vOut (0 = misma dirección, π = opuestos).
                // cos(θ) = vIn · vOut
                double dot = Math.Max(-1.0, Math.Min(1.0, vIn.DotProduct(vOut)));
                double theta = Math.Acos(dot);

                if (theta > thresholdRad)
                    corners.Add(i);
            }

            return corners;
        }

        /// <summary>
        /// Primera esquina recorriendo hacia atrás desde el segmento dado.
        /// Devuelve el ÍNDICE DEL VÉRTICE esquina.
        /// El segmento j va del vértice j al j+1.
        /// Para un segmento j, la esquina "anterior" es el vértice más cercano con índice ≤ j.
        /// </summary>
        private static int FindPrevCorner(List<int> corners, int segIdx, int nVerts)
        {
            // Buscamos el mayor corner ≤ segIdx; si no hay, envolver desde el final.
            int best = -1;
            foreach (int c in corners)
                if (c <= segIdx && c > best) best = c;

            if (best >= 0) return best;

            // Wrap: el mayor corner (será > segIdx) actuando como "anterior" circular.
            int max = -1;
            foreach (int c in corners) if (c > max) max = c;
            return max;
        }

        /// <summary>
        /// Primera esquina recorriendo hacia adelante desde el segmento dado.
        /// El segmento j va del vértice j al j+1.
        /// La esquina "siguiente" es el vértice más cercano con índice > j.
        /// </summary>
        private static int FindNextCorner(List<int> corners, int segIdx, int nVerts)
        {
            int best = -1;
            foreach (int c in corners)
                if (c > segIdx && (best < 0 || c < best)) best = c;

            if (best >= 0) return best;

            // Wrap: la esquina más temprana del polígono.
            int min = -1;
            foreach (int c in corners) if (min < 0 || c < min) min = c;
            return min;
        }
    }
}
