using DnsServerCore.ApplicationCommon;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;
using TechnitiumLibrary;
using TechnitiumLibrary.Net.Dns;
using TechnitiumLibrary.Net.Dns.EDnsOptions;
using TechnitiumLibrary.Net.Dns.ResourceRecords;

namespace GeoIspCn
{
    static class GeoIspCnRecordHandler
    {
        public static Task<DnsDatagram> ProcessRequestAsync(IDnsServer dnsServer, IGeoProviderLookup lookup, DnsDatagram request, IPEndPoint remoteEP, bool isRecursionAllowed, string zoneName, string appRecordName, uint appRecordTtl, string appRecordData, Func<int, int> randomNext = null)
        {
            DnsQuestionRecord question = request.Question[0];

            if (!question.Name.Equals(appRecordName, StringComparison.OrdinalIgnoreCase) && !appRecordName.StartsWith('*'))
                return Task.FromResult<DnsDatagram>(null);

            using JsonDocument jsonDocument = JsonDocument.Parse(appRecordData);
            JsonElement jsonAppRecordData = jsonDocument.RootElement;
            EDnsClientSubnetOptionData requestECS = request.GetEDnsClientSubnetOption();
            GeoRecordSelection selection = null;
            byte scopePrefixLength = 0;

            if (requestECS is not null)
            {
                GeoLookupResult ecsLookup = lookup.Lookup(requestECS.Address, requestECS.SourcePrefixLength);
                scopePrefixLength = ecsLookup.ScopePrefixLength;
                GeoRecordSelector.TrySelect(jsonAppRecordData, ecsLookup, out selection);
            }

            if (selection is null)
            {
                GeoLookupResult remoteLookup = lookup.Lookup(remoteEP.Address, 0);
                if (!GeoRecordSelector.TrySelect(jsonAppRecordData, remoteLookup, out selection))
                    return Task.FromResult<DnsDatagram>(null);

                scopePrefixLength = remoteLookup.ScopePrefixLength;
            }

            return CreateRecordResponse(dnsServer, request, isRecursionAllowed, zoneName, appRecordTtl, selection, requestECS, scopePrefixLength, randomNext ?? Random.Shared.Next);
        }

        static Task<DnsDatagram> CreateRecordResponse(IDnsServer dnsServer, DnsDatagram request, bool isRecursionAllowed, string zoneName, uint appRecordTtl, GeoRecordSelection selection, EDnsClientSubnetOptionData requestECS, byte scopePrefixLength, Func<int, int> randomNext)
        {
            JsonElement value = selection.Value;

            if (value.ValueKind == JsonValueKind.String)
                return CreateAliasResponse(dnsServer, request, isRecursionAllowed, zoneName, appRecordTtl, selection, value, DnsResourceRecordType.CNAME, requestECS, scopePrefixLength, randomNext);

            if (value.ValueKind == JsonValueKind.Array)
                return CreateAddressResponse(dnsServer, request, isRecursionAllowed, appRecordTtl, value, requestECS, scopePrefixLength);

            if (value.ValueKind != JsonValueKind.Object)
                return Task.FromResult<DnsDatagram>(null);

            DnsQuestionRecord question = request.Question[0];
            bool hasAlias = TryGetAlias(value, out JsonElement jsonAlias, out DnsResourceRecordType aliasType);
            bool hasAddress = TryGetAddressList(value, question.Type, out JsonElement jsonAddresses);

            if (hasAlias && hasAddress)
            {
                if (randomNext(2) == 0)
                    return CreateAliasResponse(dnsServer, request, isRecursionAllowed, zoneName, appRecordTtl, selection, jsonAlias, aliasType, requestECS, scopePrefixLength, randomNext);

                return CreateAddressResponse(dnsServer, request, isRecursionAllowed, appRecordTtl, jsonAddresses, requestECS, scopePrefixLength);
            }

            if (hasAlias)
                return CreateAliasResponse(dnsServer, request, isRecursionAllowed, zoneName, appRecordTtl, selection, jsonAlias, aliasType, requestECS, scopePrefixLength, randomNext);

            if (hasAddress)
                return CreateAddressResponse(dnsServer, request, isRecursionAllowed, appRecordTtl, jsonAddresses, requestECS, scopePrefixLength);

            return Task.FromResult<DnsDatagram>(null);
        }

        static bool TryGetAlias(JsonElement value, out JsonElement jsonAlias, out DnsResourceRecordType aliasType)
        {
            if (value.TryGetProperty("CNAME", out jsonAlias))
            {
                aliasType = DnsResourceRecordType.CNAME;
                return true;
            }

            if (value.TryGetProperty("ANAME", out jsonAlias))
            {
                aliasType = DnsResourceRecordType.ANAME;
                return true;
            }

            aliasType = default;
            return false;
        }

        static bool TryGetAddressList(JsonElement value, DnsResourceRecordType questionType, out JsonElement jsonAddresses)
        {
            if ((questionType == DnsResourceRecordType.A) && value.TryGetProperty("A", out jsonAddresses))
                return true;

            if ((questionType == DnsResourceRecordType.AAAA) && value.TryGetProperty("AAAA", out jsonAddresses))
                return true;

            jsonAddresses = default;
            return false;
        }

        static Task<DnsDatagram> CreateAddressResponse(IDnsServer dnsServer, DnsDatagram request, bool isRecursionAllowed, uint appRecordTtl, JsonElement jsonAddresses, EDnsClientSubnetOptionData requestECS, byte scopePrefixLength)
        {
            DnsQuestionRecord question = request.Question[0];

            if ((question.Type != DnsResourceRecordType.A) && (question.Type != DnsResourceRecordType.AAAA))
                return Task.FromResult<DnsDatagram>(null);

            if (jsonAddresses.ValueKind != JsonValueKind.Array)
                return Task.FromResult<DnsDatagram>(null);

            List<DnsResourceRecord> answers = new List<DnsResourceRecord>();

            foreach (JsonElement jsonAddress in jsonAddresses.EnumerateArray())
            {
                if (!IPAddress.TryParse(jsonAddress.GetString(), out IPAddress address))
                    continue;

                if ((question.Type == DnsResourceRecordType.A) && (address.AddressFamily == AddressFamily.InterNetwork))
                {
                    answers.Add(new DnsResourceRecord(question.Name, DnsResourceRecordType.A, DnsClass.IN, appRecordTtl, new DnsARecordData(address)));
                    continue;
                }

                if ((question.Type == DnsResourceRecordType.AAAA) && (address.AddressFamily == AddressFamily.InterNetworkV6))
                    answers.Add(new DnsResourceRecord(question.Name, DnsResourceRecordType.AAAA, DnsClass.IN, appRecordTtl, new DnsAAAARecordData(address)));
            }

            if (answers.Count == 0)
                return Task.FromResult<DnsDatagram>(null);

            if (answers.Count > 1)
                answers.Shuffle();

            return Task.FromResult(CreateResponse(dnsServer, request, isRecursionAllowed, answers, requestECS, scopePrefixLength));
        }

        static Task<DnsDatagram> CreateAliasResponse(IDnsServer dnsServer, DnsDatagram request, bool isRecursionAllowed, string zoneName, uint appRecordTtl, GeoRecordSelection selection, JsonElement jsonDomainName, DnsResourceRecordType aliasType, EDnsClientSubnetOptionData requestECS, byte scopePrefixLength, Func<int, int> randomNext)
        {
            DnsQuestionRecord question = request.Question[0];

            string domainName = SelectAliasDomainName(jsonDomainName, randomNext);
            if (string.IsNullOrWhiteSpace(domainName))
                return Task.FromResult<DnsDatagram>(null);

            if (!string.IsNullOrWhiteSpace(selection.CountryCode))
                domainName = domainName.Replace("{CountryCode}", selection.CountryCode, StringComparison.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(selection.ProviderKey))
                domainName = domainName.Replace("{ProviderKey}", selection.ProviderKey, StringComparison.OrdinalIgnoreCase);

            domainName = NormalizeAliasDomainName(domainName);

            DnsResourceRecord answer;

            if ((aliasType == DnsResourceRecordType.ANAME) || question.Name.Equals(zoneName, StringComparison.OrdinalIgnoreCase))
                answer = new DnsResourceRecord(question.Name, DnsResourceRecordType.ANAME, DnsClass.IN, appRecordTtl, new DnsANAMERecordData(domainName));
            else
                answer = new DnsResourceRecord(question.Name, DnsResourceRecordType.CNAME, DnsClass.IN, appRecordTtl, new DnsCNAMERecordData(domainName));

            return Task.FromResult(CreateResponse(dnsServer, request, isRecursionAllowed, new DnsResourceRecord[] { answer }, requestECS, scopePrefixLength));
        }

        static string NormalizeAliasDomainName(string domainName)
        {
            if (domainName.EndsWith(".", StringComparison.Ordinal))
                return domainName.Substring(0, domainName.Length - 1);

            return domainName;
        }

        static string SelectAliasDomainName(JsonElement jsonDomainName, Func<int, int> randomNext)
        {
            if (jsonDomainName.ValueKind == JsonValueKind.String)
                return jsonDomainName.GetString();

            if (jsonDomainName.ValueKind != JsonValueKind.Array)
                return null;

            List<string> domainNames = new List<string>();

            foreach (JsonElement jsonItem in jsonDomainName.EnumerateArray())
            {
                if (jsonItem.ValueKind != JsonValueKind.String)
                    continue;

                string domainName = jsonItem.GetString();
                if (!string.IsNullOrWhiteSpace(domainName))
                    domainNames.Add(domainName);
            }

            return domainNames.Count switch
            {
                0 => null,
                1 => domainNames[0],
                _ => domainNames[randomNext(domainNames.Count)]
            };
        }

        static DnsDatagram CreateResponse(IDnsServer dnsServer, DnsDatagram request, bool isRecursionAllowed, IReadOnlyList<DnsResourceRecord> answers, EDnsClientSubnetOptionData requestECS, byte scopePrefixLength)
        {
            EDnsOption[] options = null;

            if (requestECS is not null)
                options = EDnsClientSubnetOptionData.GetEDnsClientSubnetOption(requestECS.SourcePrefixLength, scopePrefixLength, requestECS.Address);

            return new DnsDatagram(request.Identifier, true, request.OPCODE, true, false, request.RecursionDesired, isRecursionAllowed, false, false, DnsResponseCode.NoError, request.Question, answers, null, null, dnsServer.UdpPayloadSize, EDnsHeaderFlags.None, options);
        }
    }
}
