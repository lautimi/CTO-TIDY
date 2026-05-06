using System.Collections.Generic;

namespace Koovra.Cto.AutocadAddin.Models
{
    public class AddinSettings
    {
        private static readonly AddinSettings _current = new AddinSettings();
        public static AddinSettings Current => _current;

        // Bloque para CTOs de Despliegue Inicial (40%)
        public string BlockNameDesp { get; set; } = "CAJA_ACCESO_b";

        // Bloque para CTOs de Crecimiento Futuro (100%)
        public string BlockNameCrec { get; set; } = "CAJA_CRECIMIENTO";

        // Capa donde se insertan los bloques CTO (se crea si no existe)
        // Mantenida por compatibilidad — los nuevos deploys usan CtoLayerNameDesp/Crec.
        public string CtoLayerName { get; set; } = "CTO_DESPLIEGUE";

        // Capas separadas por tipo de caja
        public string CtoLayerNameDesp { get; set; } = "CAJA ACCESO b";
        public string CtoLayerNameCrec { get; set; } = "CAJA ACCESO b-PR";

        public double TextBufferRadius { get; set; } = Geometry.GeometryConstants.TEXT_BUFFER_DEFAULT;

        // Layers de los cuales SelectionService filtra postes. No persiste entre sesiones.
        public List<string> PoleLayerNames { get; set; } = BuildDefaultPoleLayerNames();

        // Códigos que, si aparecen en COMENTARIOS de un poste, lo empujan al final del ranking PRIORIDAD.
        // No persiste entre sesiones.
        public List<string> ObservationCodes { get; set; } = BuildDefaultObservationCodes();

        public static List<string> BuildDefaultPoleLayerNames()
        {
            return new List<string> { "POSTE_*" };
        }

        public static List<string> BuildDefaultObservationCodes()
        {
            return new List<string>
            {
                "VEG", "FDR", "SUBIDA-BAJADA", "SUBIDA", "BAJADA",
                "FM", "OCUPADO", "SC", "APOYO", "INCLINADO",
                "PRIORIDAD", "SECUNDARIA", "BUENO", "MALO",
            };
        }

        public void ResetToDefaults()
        {
            PoleLayerNames   = BuildDefaultPoleLayerNames();
            ObservationCodes = BuildDefaultObservationCodes();
        }
    }
}
