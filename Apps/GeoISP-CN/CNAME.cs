/*
Technitium DNS Server
Copyright (C) 2025  Shreyas Zare (shreyas@technitium.com)

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.

*/

using DnsServerCore.ApplicationCommon;
using System;
using System.Net;
using System.Threading.Tasks;
using TechnitiumLibrary.Net.Dns;

namespace GeoIspCn
{
    public sealed class CNAME : IDnsApplication, IDnsAppRecordRequestHandler
    {
        #region variables

        IDnsServer _dnsServer;
        MaxMind _maxMind;
        IGeoProviderLookup _lookup;

        #endregion

        #region IDisposable

        bool _disposed;

        private void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                if (_maxMind is not null)
                    _maxMind.Dispose();
            }

            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        #endregion

        #region public

        public CNAME()
        { }

        internal CNAME(IGeoProviderLookup lookup = null)
        {
            _lookup = lookup;
        }

        public Task InitializeAsync(IDnsServer dnsServer, string config)
        {
            _dnsServer = dnsServer;

            if (_lookup is null)
            {
                _maxMind = MaxMind.Create(dnsServer);
                _lookup = new MaxMindLookup(_maxMind);
            }

            return Task.CompletedTask;
        }

        public Task<DnsDatagram> ProcessRequestAsync(DnsDatagram request, IPEndPoint remoteEP, DnsTransportProtocol protocol, bool isRecursionAllowed, string zoneName, string appRecordName, uint appRecordTtl, string appRecordData)
        {
            return GeoIspCnRecordHandler.ProcessRequestAsync(_dnsServer, _lookup, request, remoteEP, isRecursionAllowed, zoneName, appRecordName, appRecordTtl, appRecordData);
        }

        #endregion

        #region properties

        public string Description
        { get { return GeoIspCnAppMetadata.Description; } }

        public string ApplicationRecordDataTemplate
        {
            get
            {
                return GeoIspCnAppMetadata.ApplicationRecordDataTemplate;
            }
        }

        #endregion
    }
}
