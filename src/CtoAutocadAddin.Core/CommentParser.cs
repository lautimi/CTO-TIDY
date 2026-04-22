using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Koovra.Cto.Core
{
    /// <summary>
    /// Normaliza y parsea los textos capturados alrededor de un poste (códigos como VEG, FDR,
    /// SUBIDA-BAJADA, FM, OCUPADO, SC, APOYO, INCLINADO, etc.) y extrae el valor de HP si el
    /// texto sigue el patrón "HP=NN" o "HP NN".
    /// </summary>
    public static class CommentParser
    {
        private static readonly HashSet<string> KnownCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "VEG", "FDR", "SUBIDA-BAJADA", "SUBIDA", "BAJADA",
            "FM", "OCUPADO", "SC", "APOYO", "INCLINADO",
            "PRIORIDAD", "SECUNDARIA", "BUENO", "MALO",
        };

        private static readonly Regex HpPattern = new Regex(
            @"\bHP\s*[:=]?\s*(\d{1,3})\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static string Normalize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            return raw.Trim().ToUpperInvariant();
        }

        public static IEnumerable<string> ExtractKnownCodes(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) yield break;
            string normalized = Normalize(text);
            foreach (string code in KnownCodes)
            {
                if (normalized.Contains(code)) yield return code;
            }
        }

        /// <summary>
        /// Intenta extraer un HP numérico del texto. Retorna null si no hay match.
        /// Ejemplos que matchean: "HP=12", "HP 20", "HP:3".
        /// </summary>
        public static int? TryExtractHp(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            Match m = HpPattern.Match(text);
            if (!m.Success) return null;
            if (int.TryParse(m.Groups[1].Value, out int hp) && hp >= 0) return hp;
            return null;
        }

        /// <summary>
        /// Une múltiples comentarios separados por ";" eliminando duplicados y espacios.
        /// </summary>
        public static string JoinCsv(IEnumerable<string> comments)
        {
            if (comments == null) return string.Empty;
            return string.Join(";", comments
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase));
        }
    }
}
