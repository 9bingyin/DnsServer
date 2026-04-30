namespace GeoIspCn
{
    static class GeoIspCnAppMetadata
    {
        public const string Description = "Returns A or AAAA records, or CNAME records based on the country and ISP/ASN the client queries from. Queries GeoIP2-ISP-CN.mmdb first for CN carrier prefixes, then falls back to MaxMind GeoIP2 ISP/GeoLite2 ASN data. Supports mixed APP record branches where one branch can return IP addresses and another branch can return a domain name. Use the two-character ISO 3166-1 alpha code for the country. Provider keys are normalized to lowercase, whitespace is replaced with '-', and punctuation is removed. You can use '{CountryCode}' and '{ProviderKey}' variables in domain name branches.";

        public const string ApplicationRecordDataTemplate = @"{
  ""IN"": {
    ""providers"": {
      ""ct"": ""154.39.66.250"",
      ""chinatelecom"": ""154.39.66.250"",
      ""default"": [
        ""82.26.72.199"",
        ""82.26.72.200""
      ]
    }
  },
  ""default"": ""global.example.com""
}";
    }
}
