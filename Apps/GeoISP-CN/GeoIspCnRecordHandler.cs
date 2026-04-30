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
        public static Task<DnsDatagram> ProcessRequestAsync(IDnsServer dnsServer, IGeoProviderLookup lookup, DnsDatagram request, IPEndPoint remoteEP, bool isRecursionAllowed, string zoneName, string appRecordName, uint appRecordTtl, string appRecordData)
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
            }

            return selection.Value.ValueKind switch
            {
                JsonValueKind.Array => CreateAddressResponse(dnsServer, request, isRecursionAllowed, appRecordTtl, selection.Value, requestECS, scopePrefixLength),
                JsonValueKind.String => CreateAliasResponse(dnsServer, request, isRecursionAllowed, zoneName, appRecordTtl, selection, requestECS, scopePrefixLength),
                _ => Task.FromResult<DnsDatagram>(null)
            };
        }

        static Task<DnsDatagram> CreateAddressResponse(IDnsServer dnsServer, DnsDatagram request, bool isRecursionAllowed, uint appRecordTtl, JsonElement jsonAddresses, EDnsClientSubnetOptionData requestECS, byte scopePrefixLength)
        {
            DnsQuestionRecord question = request.Question[0];

            if ((question.Type != DnsResourceRecordType.A) && (question.Type != DnsResourceRecordType.AAAA))
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

        static Task<DnsDatagram> CreateAliasResponse(IDnsServer dnsServer, DnsDatagram request, bool isRecursionAllowed, string zoneName, uint appRecordTtl, GeoRecordSelection selection, EDnsClientSubnetOptionData requestECS, byte scopePrefixLength)
        {
            DnsQuestionRecord question = request.Question[0];

            string domainName = selection.Value.GetString();
            if (string.IsNullOrWhiteSpace(domainName))
                return Task.FromResult<DnsDatagram>(null);

            if (!string.IsNullOrWhiteSpace(selection.CountryCode))
                domainName = domainName.Replace("{CountryCode}", selection.CountryCode, StringComparison.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(selection.ProviderKey))
                domainName = domainName.Replace("{ProviderKey}", selection.ProviderKey, StringComparison.OrdinalIgnoreCase);

            DnsResourceRecord answer;

            if (question.Name.Equals(zoneName, StringComparison.OrdinalIgnoreCase))
                answer = new DnsResourceRecord(question.Name, DnsResourceRecordType.ANAME, DnsClass.IN, appRecordTtl, new DnsANAMERecordData(domainName));
            else
                answer = new DnsResourceRecord(question.Name, DnsResourceRecordType.CNAME, DnsClass.IN, appRecordTtl, new DnsCNAMERecordData(domainName));

            return Task.FromResult(CreateResponse(dnsServer, request, isRecursionAllowed, new DnsResourceRecord[] { answer }, requestECS, scopePrefixLength));
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
