namespace Koovra.Cto.AutocadAddin.Persistence
{
    public static class AppNames
    {
        public const string KOOVRA_CTO = "KOOVRA_CTO";
    }

    public static class XDataKeys
    {
        public const string ID_SEGMENT  = "ID_SEGMENT";
        public const string COMENTARIOS = "COMENTARIOS";
        public const string HP          = "HP";
        public const string LARGO       = "LARGO";
        public const string C_DESP      = "C_DESP";
        public const string C_CREC      = "C_CREC";
        public const string C_DESP_OVF  = "C_DESP_OVF";  // overflow D de este segmento (escrito por Paso 4, leído por Paso 5)
        public const string C_CREC_OVF  = "C_CREC_OVF";  // overflow C de este segmento
        public const string REVISAR     = "REVISAR";

        // ── Linga de acero (paso 2) ──────────────────────────────────────────
        public const string ID_LINGA    = "ID_LINGA";     // handle hex de la Line asociada
        public const string LINGA_TIPO  = "LINGA_TIPO";   // "PRIORIDAD" | "SECUNDARIA" | ""
        public const string LARGO_LINGA = "LARGO_LINGA";  // (double) largo real de la linga en m

        // ── Frente de manzana (paso 2) ───────────────────────────────────────
        // El LARGO que entra en la tabla oficial CTO sale de acá, no de LARGO_LINGA.
        // La linga sólo decide qué frente recibe cajas (el de PRIORIDAD).
        public const string ID_FRENTE    = "ID_FRENTE";     // "<manzanaHandle>#<frenteIdx>"
        public const string LARGO_FRENTE = "LARGO_FRENTE";  // (double) largo del lado de la manzana entre dos esquinas

        // ── Valores válidos de LINGA_TIPO ────────────────────────────────────
        public const string LINGA_PRIORIDAD  = "PRIORIDAD";
        public const string LINGA_SECUNDARIA = "SECUNDARIA";
    }
}
