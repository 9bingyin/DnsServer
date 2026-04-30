#nullable enable

using System.Collections.Generic;

namespace GeoIspCn
{
    static class ProviderKeyCollector
    {
        public static void Add(List<string> providerKeys, HashSet<string> seenProviderKeys, string? value)
        {
            string? providerKey = ProviderKeyNormalizer.Normalize(value);
            if ((providerKey is null) || !seenProviderKeys.Add(providerKey))
                return;

            providerKeys.Add(providerKey);
        }
    }
}
