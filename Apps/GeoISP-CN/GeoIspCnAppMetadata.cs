namespace GeoIspCn
{
    static class GeoIspCnAppMetadata
    {
        public const string Description = "Returns A, AAAA, CNAME, or ANAME records based on the country and ISP/ASN the client queries from. Queries GeoIP2-ISP-CN.mmdb first for CN carrier prefixes, then falls back to MaxMind GeoIP2 ISP/GeoLite2 ASN data. Each branch uses one JSON record object; when CNAME/ANAME and the requested A/AAAA type both exist, one response type is selected randomly at 1:1 and never returned together. CNAME and ANAME can be a single absolute domain name or an array of absolute domain names selected randomly at equal weight. Use the two-character ISO 3166-1 alpha code for the country. Provider keys are normalized to lowercase, whitespace is replaced with '-', and punctuation is removed. You can use '{CountryCode}' and '{ProviderKey}' variables in alias branches.";

        public const string ApplicationRecordDataTemplate = @"{
  ""CN"": {
    ""ct"": {
      ""A"": [""154.39.66.250""],
      ""CNAME"": [
        ""ct1.example.com."",
        ""ct2.example.com.""
      ]
    },
    ""chinatelecom"": {
      ""A"": [""154.39.66.250""]
    },
    ""default"": {
      ""A"": [
        ""82.26.72.199"",
        ""82.26.72.200""
      ],
      ""AAAA"": [
        ""2001:db8::1""
      ]
    }
  },
  ""default"": {
    ""CNAME"": [
      ""global1.example.com."",
      ""global2.example.com.""
    ]
  }
}";
    }
}
