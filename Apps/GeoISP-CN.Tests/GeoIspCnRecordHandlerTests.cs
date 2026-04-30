using DnsServerCore.ApplicationCommon;
using GeoIspCn;
using System.Net;
using System.Net.Mail;
using TechnitiumLibrary.Net.Dns;
using TechnitiumLibrary.Net.Dns.ResourceRecords;
using TechnitiumLibrary.Net.Proxy;
using Xunit;

namespace GeoIspCnTests;

public sealed class GeoIspCnRecordHandlerTests
{
    const string ZoneName = "example.com";
    const string AppRecordName = "www.example.com";
    const uint Ttl = 300;

    static readonly FakeDnsServer DnsServer = new();
    static readonly IPEndPoint RemoteEP = new(IPAddress.Parse("203.0.113.10"), 53000);

    [Fact]
    public async Task Selects_flat_provider_a_record()
    {
        string appRecordData = """
        {
          "CN": {
            "ct": {
              "A": ["154.39.66.250"]
            },
            "default": {
              "A": ["82.26.72.199"]
            }
          },
          "default": {
            "A": ["2.2.2.2"]
          }
        }
        """;

        DnsDatagram response = await ProcessAsync(appRecordData, DnsResourceRecordType.A, Lookup("CN", "ct"));

        DnsResourceRecord answer = Assert.Single(response.Answer);
        Assert.Equal(DnsResourceRecordType.A, answer.Type);
        Assert.Equal(IPAddress.Parse("154.39.66.250"), Assert.IsType<DnsARecordData>(answer.RDATA).Address);
    }

    [Fact]
    public async Task Falls_back_to_country_default_provider()
    {
        string appRecordData = """
        {
          "CN": {
            "ct": {
              "A": ["154.39.66.250"]
            },
            "default": {
              "A": ["82.26.72.199", "82.26.72.200"]
            }
          },
          "default": {
            "A": ["2.2.2.2"]
          }
        }
        """;

        DnsDatagram response = await ProcessAsync(appRecordData, DnsResourceRecordType.A, Lookup("CN", "cu"));

        Assert.Equal(new[] { "82.26.72.199", "82.26.72.200" }, response.Answer.Select(answer => Assert.IsType<DnsARecordData>(answer.RDATA).Address.ToString()).Order());
    }

    [Fact]
    public async Task Supports_legacy_providers_wrapper()
    {
        string appRecordData = """
        {
          "CN": {
            "providers": {
              "ct": {
                "A": ["154.39.66.250"]
              }
            }
          },
          "default": {
            "A": ["2.2.2.2"]
          }
        }
        """;

        DnsDatagram response = await ProcessAsync(appRecordData, DnsResourceRecordType.A, Lookup("CN", "ct"));

        DnsResourceRecord answer = Assert.Single(response.Answer);
        Assert.Equal(IPAddress.Parse("154.39.66.250"), Assert.IsType<DnsARecordData>(answer.RDATA).Address);
    }

    [Fact]
    public async Task Returns_aaaa_records_for_aaaa_query()
    {
        string appRecordData = """
        {
          "CN": {
            "ct": {
              "A": ["154.39.66.250"],
              "AAAA": ["2001:db8::1"]
            }
          }
        }
        """;

        DnsDatagram response = await ProcessAsync(appRecordData, DnsResourceRecordType.AAAA, Lookup("CN", "ct"));

        DnsResourceRecord answer = Assert.Single(response.Answer);
        Assert.Equal(DnsResourceRecordType.AAAA, answer.Type);
        Assert.Equal(IPAddress.Parse("2001:db8::1"), Assert.IsType<DnsAAAARecordData>(answer.RDATA).Address);
    }

    [Fact]
    public async Task Selects_cname_target_from_array_by_equal_weight()
    {
        string appRecordData = """
        {
          "CN": {
            "ct": {
              "CNAME": [
                "ct1.example.com.",
                "ct2.example.com."
              ]
            }
          }
        }
        """;

        DnsDatagram response = await ProcessAsync(appRecordData, DnsResourceRecordType.A, Lookup("CN", "ct"), _ => 1);

        DnsResourceRecord answer = Assert.Single(response.Answer);
        Assert.Equal(DnsResourceRecordType.CNAME, answer.Type);
        Assert.Equal("ct2.example.com", Assert.IsType<DnsCNAMERecordData>(answer.RDATA).Domain);
    }

    [Fact]
    public async Task Selects_one_response_type_when_a_and_cname_both_exist()
    {
        string appRecordData = """
        {
          "CN": {
            "ct": {
              "A": ["154.39.66.250"],
              "CNAME": ["ct1.example.com.", "ct2.example.com."]
            }
          }
        }
        """;

        DnsDatagram cnameResponse = await ProcessAsync(appRecordData, DnsResourceRecordType.A, Lookup("CN", "ct"), _ => 0);
        DnsDatagram addressResponse = await ProcessAsync(appRecordData, DnsResourceRecordType.A, Lookup("CN", "ct"), _ => 1);

        DnsResourceRecord cnameAnswer = Assert.Single(cnameResponse.Answer);
        DnsResourceRecord addressAnswer = Assert.Single(addressResponse.Answer);

        Assert.Equal(DnsResourceRecordType.CNAME, cnameAnswer.Type);
        Assert.Equal(DnsResourceRecordType.A, addressAnswer.Type);
    }

    [Fact]
    public async Task Uses_cname_when_aaaa_query_has_no_aaaa_candidate()
    {
        string appRecordData = """
        {
          "CN": {
            "ct": {
              "A": ["154.39.66.250"],
              "CNAME": "ct.example.com."
            }
          }
        }
        """;

        DnsDatagram response = await ProcessAsync(appRecordData, DnsResourceRecordType.AAAA, Lookup("CN", "ct"));

        DnsResourceRecord answer = Assert.Single(response.Answer);
        Assert.Equal(DnsResourceRecordType.CNAME, answer.Type);
        Assert.Equal("ct.example.com", Assert.IsType<DnsCNAMERecordData>(answer.RDATA).Domain);
    }

    [Fact]
    public async Task Converts_apex_cname_to_aname()
    {
        string appRecordData = """
        {
          "default": {
            "CNAME": "global.example.com."
          }
        }
        """;

        DnsDatagram response = await ProcessAsync(appRecordData, DnsResourceRecordType.A, Lookup(null), appRecordName: ZoneName);

        DnsResourceRecord answer = Assert.Single(response.Answer);
        Assert.Equal(DnsResourceRecordType.ANAME, answer.Type);
        Assert.Equal("global.example.com", Assert.IsType<DnsANAMERecordData>(answer.RDATA).Domain);
    }

    [Fact]
    public async Task Returns_null_when_no_country_or_default_matches()
    {
        string appRecordData = """
        {
          "US": {
            "A": ["1.1.1.1"]
          }
        }
        """;

        DnsDatagram? response = await ProcessAsync(appRecordData, DnsResourceRecordType.A, Lookup("CN", "ct"));

        Assert.Null(response);
    }

    static Task<DnsDatagram> ProcessAsync(string appRecordData, DnsResourceRecordType questionType, GeoLookupResult lookupResult, Func<int, int>? randomNext = null, string appRecordName = AppRecordName)
    {
        DnsDatagram request = new(1234, false, DnsOpcode.StandardQuery, false, false, true, false, false, false, DnsResponseCode.NoError, [new DnsQuestionRecord(appRecordName, questionType, DnsClass.IN)]);
        return GeoIspCnRecordHandler.ProcessRequestAsync(DnsServer, new FakeGeoProviderLookup(lookupResult), request, RemoteEP, true, ZoneName, appRecordName, Ttl, appRecordData, randomNext);
    }

    static GeoLookupResult Lookup(string? countryCode, params string[] providerKeys)
    {
        return new GeoLookupResult(countryCode, providerKeys, 24);
    }

    sealed class FakeGeoProviderLookup : IGeoProviderLookup
    {
        readonly GeoLookupResult _lookupResult;

        public FakeGeoProviderLookup(GeoLookupResult lookupResult)
        {
            _lookupResult = lookupResult;
        }

        public GeoLookupResult Lookup(IPAddress address, byte fallbackScopePrefixLength)
        {
            return _lookupResult;
        }
    }

    sealed class FakeDnsServer : IDnsServer
    {
        public string ApplicationName => "GeoISP-CN";

        public string ApplicationFolder => AppContext.BaseDirectory;

        public string ServerDomain => "dns.example.com";

        public MailAddress ResponsiblePerson { get; } = new("hostmaster@example.com");

        public IDnsCache DnsCache => null!;

        public NetProxy? Proxy => null;

        public IPv6Mode IPv6Mode => IPv6Mode.Enabled;

        public ushort UdpPayloadSize => DnsDatagram.EDNS_DEFAULT_UDP_PAYLOAD_SIZE;

        public Task<DnsDatagram> DirectQueryAsync(DnsQuestionRecord question, int timeout = 4000, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<DnsDatagram> DirectQueryAsync(DnsDatagram request, int timeout = 4000, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<DnsDatagram> ResolveAsync(DnsQuestionRecord question, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public void WriteLog(string message)
        { }

        public void WriteLog(Exception ex)
        { }
    }
}
