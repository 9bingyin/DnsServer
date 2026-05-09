#nullable enable

using IP2Region.Net.Abstractions;
using System;
using System.Collections.Generic;
using System.Net;

namespace GeoIspCn
{
    static class Ip2RegionXdbProviderLookup
    {
        const int IspFieldIndex = 5;
        const int AsnFieldIndex = 13;

        public static bool TryLookup(ISearcher searcher, IPAddress address, List<string> providerKeys, HashSet<string> seenProviderKeys, byte fallbackScopePrefixLength, out string? countryCode, out byte scopePrefixLength)
        {
            countryCode = null;
            scopePrefixLength = fallbackScopePrefixLength;

            string? region = searcher.Search(address.ToString());
            return TryParse(region, providerKeys, seenProviderKeys, out countryCode);
        }

        internal static bool TryParse(string? region, List<string> providerKeys, HashSet<string> seenProviderKeys, out string? countryCode)
        {
            countryCode = null;

            if (string.IsNullOrWhiteSpace(region))
                return false;

            string[] fields = region.Split('|');
            countryCode = GetCountryCode(fields);

            string? isp = GetField(fields, IspFieldIndex);
            AddProvider(providerKeys, seenProviderKeys, isp);

            string? asn = GetField(fields, AsnFieldIndex);
            if (!string.IsNullOrWhiteSpace(asn))
                ProviderKeyCollector.Add(providerKeys, seenProviderKeys, asn);

            return (countryCode is not null) || (providerKeys.Count > 0);
        }

        static string? GetCountryCode(string[] fields)
        {
            for (int i = fields.Length - 1; i >= 0; i--)
            {
                string? value = GetField(fields, i);
                if ((value is not null) && (value.Length == 2) && char.IsLetter(value[0]) && char.IsLetter(value[1]))
                    return value.ToUpperInvariant();
            }

            return null;
        }

        static string? GetField(string[] fields, int index)
        {
            if (index >= fields.Length)
                return null;

            string value = fields[index].Trim();
            return value.Length == 0 ? null : value;
        }

        static void AddProvider(List<string> providerKeys, HashSet<string> seenProviderKeys, string? provider)
        {
            ProviderKeyCollector.Add(providerKeys, seenProviderKeys, provider);

            if (string.IsNullOrWhiteSpace(provider))
                return;

            foreach (string alias in GetCnProviderAliases(provider))
                ProviderKeyCollector.Add(providerKeys, seenProviderKeys, alias);
        }

        static IReadOnlyList<string> GetCnProviderAliases(string provider)
        {
            if (provider.Contains("电信", StringComparison.OrdinalIgnoreCase) || provider.Contains("China Telecom", StringComparison.OrdinalIgnoreCase))
                return new[] { "ct", "chinatelecom" };

            if (provider.Contains("联通", StringComparison.OrdinalIgnoreCase) || provider.Contains("China Unicom", StringComparison.OrdinalIgnoreCase))
                return new[] { "cu", "chinaunicom" };

            if (provider.Contains("移动", StringComparison.OrdinalIgnoreCase) || provider.Contains("China Mobile", StringComparison.OrdinalIgnoreCase))
                return new[] { "cm", "chinamobile", "cmi" };

            if (provider.Contains("广电", StringComparison.OrdinalIgnoreCase) || provider.Contains("广播电视", StringComparison.OrdinalIgnoreCase))
                return new[] { "cbn", "chinabroadnet", "chinabroadcast" };

            if (provider.Contains("教育网", StringComparison.OrdinalIgnoreCase) || provider.Contains("CERNET", StringComparison.OrdinalIgnoreCase))
                return new[] { "cernet", "chinaeducation" };

            return Array.Empty<string>();
        }
    }
}
