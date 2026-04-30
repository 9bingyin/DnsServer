#nullable enable

using System.Text;

namespace GeoIspCn
{
    static class ProviderKeyNormalizer
    {
        public static string? Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            StringBuilder normalized = new StringBuilder(value.Length);
            bool previousDash = false;

            foreach (char c in value.Trim())
            {
                if (char.IsLetterOrDigit(c))
                {
                    normalized.Append(char.ToLowerInvariant(c));
                    previousDash = false;
                    continue;
                }

                if (char.IsWhiteSpace(c) || (c == '-') || (c == '_'))
                {
                    if ((normalized.Length > 0) && !previousDash)
                    {
                        normalized.Append('-');
                        previousDash = true;
                    }
                }
            }

            while ((normalized.Length > 0) && (normalized[normalized.Length - 1] == '-'))
                normalized.Length--;

            return normalized.Length == 0 ? null : normalized.ToString();
        }
    }
}
