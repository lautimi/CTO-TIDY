using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Koovra.Cto.AutocadAddin.Geometry;

namespace Koovra.Cto.AutocadAddin.Services
{
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
            if (segmentCurve != null)
            {
                Point3d cA    = manzana.GetPoint3dAt(startCorner);
                Point3d cB    = manzana.GetPoint3dAt(endCorner);
                Point3d projA = segmentCurve.GetClosestPointTo(cA, false);
                Point3d projB = segmentCurve.GetClosestPointTo(cB, false);
                double  dA    = segmentCurve.GetDistAtPoint(projA);
                double  dB    = segmentCurve.GetDistAtPoint(projB);
                largo = Math.Abs(dB - dA);
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
            };
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
