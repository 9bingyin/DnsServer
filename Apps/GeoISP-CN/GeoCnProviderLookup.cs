#nullable enable

using MaxMind.Db;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace GeoIspCn
{
    static class GeoCnProviderLookup
    {
        public static bool TryLookup(Reader reader, IPAddress address, List<string> providerKeys, HashSet<string> seenProviderKeys, out byte scopePrefixLength)
        {
            Dictionary<string, object>? record = reader.Find<Dictionary<string, object>>(address, out int prefixLength, null);
            if (record is null)
            {
                scopePrefixLength = 0;
                return false;
            }

            ProviderKeyCollector.Add(providerKeys, seenProviderKeys, GetString(record, "provider"));
            ProviderKeyCollector.Add(providerKeys, seenProviderKeys, GetString(record, "provider_name"));

            foreach (string alias in GetStrings(record, "aliases"))
            {
                ProviderKeyCollector.Add(providerKeys, seenProviderKeys, alias);
            }

            scopePrefixLength = (byte)prefixLength;
            return providerKeys.Count > 0;
        }

        static string? GetString(Dictionary<string, object> record, string key)
        {
            if (!record.TryGetValue(key, out object? value))
                return null;

            return value as string;
        }

        static IReadOnlyList<string> GetStrings(Dictionary<string, object> record, string key)
        {
            if (!record.TryGetValue(key, out object? value) || (value is null))
                return Array.Empty<string>();

            return value switch
            {
                string[] strings => strings,
                IEnumerable<object> objects => objects.OfType<string>().ToArray(),
                _ => Array.Empty<string>()
            };
        }
    }
}
