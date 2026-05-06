namespace Koovra.Cto.AutocadAddin.Geometry
{
    public static class GeometryConstants
    {
        public const double RAY_LENGTH = 150.0;
        public const double ANTI_CROSS_MARGIN = 2.0;
        public const int NEAREST_NEIGHBOR_K = 10;
        public const double EPSILON_DIST = 0.01;
        public const double TEXT_BUFFER_DEFAULT = 5.0;
        public const double CTO_OFFSET_X = 0.0;
        public const double CTO_OFFSET_Y = 3.4;   // offset perpendicular al eje (hacia vereda)
        public const double CTO_SEPARACION = 0.5; // apilado a lo largo de la linga
        public const double CTO_CREC_OFFSET_ADICIONAL = 3.54;
        public const double CTO_ALERT_CIRCLE_RADIUS = 10.0;
        /// <summary>Tolerancia para considerar que dos endpoints de Lines tocan la misma esquina.</summary>
        public const double STREET_CORNER_TOLERANCE = 0.5;
        /// <summary>Distancia máxima permitida entre un endpoint de segmento y la esquina-de-calle más cercana.</summary>
        public const double STREET_CORNER_SEARCH_MAX = 10.0;
        /// <summary>Distancia máxima permitida entre una esquina-de-calle y la polilínea de manzana (sino, fallback).</summary>
        public const double CORNER_TO_MANZANA_MAX = 8.0;
        /// <summary>
        /// Distancia máxima entre el punto de intersección de dos líneas de calle
        /// y el endpoint más cercano de cada segmento, para considerarlo esquina real.
        /// Cubre cruces con plaza central (cordones) donde los endpoints de las
        /// calles transversales pueden estar separados por el ancho de la plaza.
        /// </summary>
        public const double MAX_INTERSECTION_DIST = 2.0;
    }
}
