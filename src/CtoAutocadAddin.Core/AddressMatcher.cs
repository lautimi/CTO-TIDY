using System;

namespace Koovra.Cto.Core
{
    /// <summary>
    /// Replica la lógica de auditoría de direcciones del script Python original
    /// (OK / REVISAR / SIN_CALLE_POSTE).
    /// </summary>
    public static class AddressMatcher
    {
        public enum Estado
        {
            Ok,
            Revisar,
            SinCallePoste,
            SinSegmento,
        }

        public const string OK = "OK";
        public const string REVISAR = "REVISAR";
        public const string SIN_CALLE_POSTE = "SIN_CALLE_POSTE";
        public const string SIN_SEGMENTO = "SIN_SEGMENTO";

        /// <summary>
        /// Compara la dirección del poste con la del segmento asociado:
        /// - Si no hay dirección de poste → SIN_CALLE_POSTE (pero se conserva dirección del segmento).
        /// - Si la dirección del segmento aparece como substring (case-insensitive) dentro de la del poste → OK.
        /// - En caso contrario → REVISAR.
        /// </summary>
        public static string Compare(string dirPoste, string dirSegmento)
        {
            string p = dirPoste?.Trim() ?? string.Empty;
            string s = dirSegmento?.Trim() ?? string.Empty;

            if (p.Length == 0) return SIN_CALLE_POSTE;
            if (s.Length == 0) return REVISAR;

            return p.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0 ? OK : REVISAR;
        }
    }
}
