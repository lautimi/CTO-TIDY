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
        public string CtoLayerName { get; set; } = "CTO_DESPLIEGUE";

        public double TextBufferRadius { get; set; } = Geometry.GeometryConstants.TEXT_BUFFER_DEFAULT;

        // Layer del cual SelectionService filtra postes. No persiste entre sesiones.
        public string PoleLayerName { get; set; } = DefaultPoleLayerName;

        // Códigos que, si aparecen en COMENTARIOS de un poste, lo empujan al final del ranking PRIORIDAD.
        // No persiste entre sesiones.
        public List<string> ObservationCodes { get; set; } = BuildDefaultObservationCodes();

        private const string DefaultPoleLayerName = "POSTE_*";

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
            PoleLayerName    = DefaultPoleLayerName;
            ObservationCodes = BuildDefaultObservationCodes();
        }
    }
}
