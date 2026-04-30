#nullable enable

using System.Text.Json;

namespace GeoIspCn
{
    sealed class GeoRecordSelection
    {
        public GeoRecordSelection(JsonElement value, string? countryCode, string? providerKey)
        {
            Value = value;
            CountryCode = countryCode;
            ProviderKey = providerKey;
        }

        public JsonElement Value { get; }

        public string? CountryCode { get; }

        public string? ProviderKey { get; }
    }

    static class GeoRecordSelector
    {
        public static bool TrySelect(JsonElement jsonAppRecordData, GeoLookupResult? lookupResult, out GeoRecordSelection? selection)
        {
            selection = null;

            string? normalizedCountryCode = lookupResult?.CountryCode?.ToLowerInvariant();
            string fallbackCountryCode = normalizedCountryCode ?? "default";
            string fallbackProviderKey = ((lookupResult is not null) && (lookupResult.ProviderKeys.Count > 0)) ? lookupResult.ProviderKeys[0] : "default";

            if ((lookupResult is not null) && !string.IsNullOrWhiteSpace(lookupResult.CountryCode) && jsonAppRecordData.TryGetProperty(lookupResult.CountryCode, out JsonElement jsonCountry))
            {
                if (TrySelectCountryValue(jsonCountry, lookupResult, fallbackCountryCode, out selection))
                    return true;
            }

            if (jsonAppRecordData.TryGetProperty("default", out JsonElement jsonDefault))
            {
                if (IsSupportedValue(jsonDefault))
                {
                    selection = new GeoRecordSelection(jsonDefault.Clone(), fallbackCountryCode, fallbackProviderKey);
                    return true;
                }
            }

            return false;
        }

        static bool TrySelectCountryValue(JsonElement jsonCountry, GeoLookupResult lookupResult, string normalizedCountryCode, out GeoRecordSelection? selection)
        {
            selection = null;

            if ((jsonCountry.ValueKind == JsonValueKind.Object) && jsonCountry.TryGetProperty("providers", out JsonElement jsonProviders) && (jsonProviders.ValueKind == JsonValueKind.Object))
            {
                foreach (string providerKey in lookupResult.ProviderKeys)
                {
                    if (jsonProviders.TryGetProperty(providerKey, out JsonElement jsonProviderValue) && IsSupportedValue(jsonProviderValue))
                    {
                        selection = new GeoRecordSelection(jsonProviderValue.Clone(), normalizedCountryCode, providerKey);
                        return true;
                    }
                }

                if (jsonProviders.TryGetProperty("default", out JsonElement jsonProviderDefault) && IsSupportedValue(jsonProviderDefault))
                {
                    selection = new GeoRecordSelection(jsonProviderDefault.Clone(), normalizedCountryCode, "default");
                    return true;
                }

                return false;
            }

            if ((jsonCountry.ValueKind == JsonValueKind.Array) || (jsonCountry.ValueKind == JsonValueKind.String))
            {
                selection = new GeoRecordSelection(jsonCountry.Clone(), normalizedCountryCode, null);
                return true;
            }

            return false;
        }

        static bool IsSupportedValue(JsonElement value)
        {
            return (value.ValueKind == JsonValueKind.Array) || (value.ValueKind == JsonValueKind.String);
        }
    }
}
