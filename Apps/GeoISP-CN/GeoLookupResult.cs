using System.Collections.Generic;

﻿#nullable enable

namespace GeoIspCn
{
    sealed class GeoLookupResult
    {
        public GeoLookupResult(string? countryCode, IReadOnlyList<string> providerKeys, byte scopePrefixLength)
        {
            CountryCode = countryCode;
            ProviderKeys = providerKeys;
            ScopePrefixLength = scopePrefixLength;
        }

        public string? CountryCode { get; }

        public IReadOnlyList<string> ProviderKeys { get; }

        public byte ScopePrefixLength { get; }
    }
}
