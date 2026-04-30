#nullable enable

using MaxMind.GeoIP2.Responses;
using System;
using System.Collections.Generic;
using System.Net;

namespace GeoIspCn
{
    interface IGeoProviderLookup
    {
        GeoLookupResult Lookup(IPAddress address, byte fallbackScopePrefixLength);
    }

    sealed class MaxMindLookup : IGeoProviderLookup
    {
        readonly MaxMind _maxMind;

        public MaxMindLookup(MaxMind maxMind)
        {
            _maxMind = maxMind ?? throw new ArgumentNullException(nameof(maxMind));
        }

        public GeoLookupResult Lookup(IPAddress address, byte fallbackScopePrefixLength)
        {
            string? countryCode = null;
            byte scopePrefixLength = fallbackScopePrefixLength;
            List<string> providerKeys = new List<string>(5);
            HashSet<string> seenProviderKeys = new HashSet<string>(StringComparer.Ordinal);

            if (_maxMind.CountryReader.TryCountry(address, out CountryResponse? countryResponse) && (countryResponse?.Country is not null))
                countryCode = countryResponse.Country.IsoCode;

            if ((_maxMind.CnIspReader is not null) && GeoCnProviderLookup.TryLookup(_maxMind.CnIspReader, address, providerKeys, seenProviderKeys, out scopePrefixLength))
                return new GeoLookupResult(countryCode, providerKeys, scopePrefixLength);

            if ((_maxMind.IspReader is not null) && _maxMind.IspReader.TryIsp(address, out IspResponse? ispResponse) && (ispResponse is not null))
            {
                if (ispResponse.Network is not null)
                    scopePrefixLength = (byte)ispResponse.Network.PrefixLength;

                ProviderKeyCollector.Add(providerKeys, seenProviderKeys, ispResponse.Isp);
                ProviderKeyCollector.Add(providerKeys, seenProviderKeys, ispResponse.Organization);

                if (ispResponse.AutonomousSystemNumber.HasValue)
                    ProviderKeyCollector.Add(providerKeys, seenProviderKeys, "as" + ispResponse.AutonomousSystemNumber.Value);

                ProviderKeyCollector.Add(providerKeys, seenProviderKeys, ispResponse.AutonomousSystemOrganization);
            }
            else if ((_maxMind.AsnReader is not null) && _maxMind.AsnReader.TryAsn(address, out AsnResponse? asnResponse) && (asnResponse is not null))
            {
                if (asnResponse.Network is not null)
                    scopePrefixLength = (byte)asnResponse.Network.PrefixLength;

                ProviderKeyCollector.Add(providerKeys, seenProviderKeys, asnResponse.AutonomousSystemOrganization);

                if (asnResponse.AutonomousSystemNumber.HasValue)
                    ProviderKeyCollector.Add(providerKeys, seenProviderKeys, "as" + asnResponse.AutonomousSystemNumber.Value);
            }

            return new GeoLookupResult(countryCode, providerKeys, scopePrefixLength);
        }
    }
}
