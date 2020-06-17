using Microsoft.Azure.Management.Dns;
using Microsoft.Azure.Management.Dns.Models;
using Microsoft.Rest.Azure.Authentication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dyndnsfunctionapp
{
    public class AzureDnsUpdater
    {
        private string clientId;
        private string tenantId;
        private string secret;
        private string subId;

        public AzureDnsUpdater(string clientId, string tenantId, string secret, string subId)
        {
            this.clientId = clientId;
            this.tenantId = tenantId;
            this.secret = secret;
            this.subId = subId;
        }

        public async Task<bool> UpdateAzureDns(string resourceGroupName, string zoneName, string ip)
        {
            var serviceCreds = await ApplicationTokenProvider.LoginSilentAsync(tenantId, clientId, secret);
            var dnsClient = new DnsManagementClient(serviceCreds);
            dnsClient.SubscriptionId = subId;

            var updated = false;
            updated = await UpdateRecordSet(zoneName, ip, dnsClient, resourceGroupName, "@");
            updated = updated || await UpdateRecordSet(zoneName, ip, dnsClient, resourceGroupName, "*");
            return updated;
        }

        private async Task<bool> UpdateRecordSet(string zoneName, string ip, DnsManagementClient dnsClient, string resourceGroupName, string recordSetName)
        {
            var recordSet = dnsClient.RecordSets.Get(resourceGroupName, zoneName, recordSetName, RecordType.A);
            if (IpChanged(ip, recordSet.ARecords.FirstOrDefault()))
            {
                recordSet.ARecords.Clear();
                recordSet.ARecords.Add(new ARecord(ip));
                await dnsClient.RecordSets.CreateOrUpdateAsync(resourceGroupName, zoneName, recordSetName, RecordType.A, recordSet, recordSet.Etag);
                return true;
            }
            return false;
        }

        private bool IpChanged(string ip, ARecord aRecord)
        {
            if (aRecord == null)
                return true;
            return aRecord.Ipv4Address != ip;

        }
    }
}
