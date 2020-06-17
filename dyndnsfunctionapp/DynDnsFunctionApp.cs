using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace dyndnsfunctionapp
{
    public class DynDnsFunctionApp
    {
        const string PROTOCOL_RESPONSE_UPDATED = "good";
        const string PROTOCOL_RESPONSE_NO_CHANGE = "nochg";

        private readonly TelemetryClient _telemetryClient;
        public DynDnsFunctionApp(TelemetryConfiguration configuration)
        {
            _telemetryClient = new TelemetryClient(configuration);
        }

        [FunctionName("update")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            var updater = GetUpdater();
            var resourceGroupName = GetEnvironmentVariable("ResourceGroupName");
            string domain = req.Query["hostname"];
            string ip = req.Query["myip"];

            var authed = Authorize(req.HttpContext);
            if (authed)
            {
                var updated = await updater.UpdateAzureDns(resourceGroupName, domain, ip);
                _telemetryClient.Context.GlobalProperties.Add("updated", updated.ToString()); //not working due to some azure functions/ai bug
                if (updated)
                {
                    //log.LogInformation($"A record for {domain} set to {ip}.");
                    return new OkObjectResult(PROTOCOL_RESPONSE_UPDATED);
                }
                else
                {
                    log.LogDebug("No changes necessary.");
                    return new OkObjectResult(PROTOCOL_RESPONSE_NO_CHANGE);
                }
                
                
            }
            return new ForbidResult();
            
        }

        private static AzureDnsUpdater GetUpdater()
        {
            var clientId = GetEnvironmentVariable("ClientId");
            var tenantId = GetEnvironmentVariable("TenantId");
            var secret = GetEnvironmentVariable("Secret");
            var subId = GetEnvironmentVariable("SubscriptionId");

            return new AzureDnsUpdater(clientId, tenantId, secret, subId);

        }

        static bool Authorize(HttpContext context)
        {
            try
            {
                string authHeader = context.Request.Headers["Authorization"];
                if (authHeader != null)
                {
                    var authHeaderValue = AuthenticationHeaderValue.Parse(authHeader);
                    if (authHeaderValue.Scheme.Equals(AuthenticationSchemes.Basic.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        var credentials = Encoding.UTF8
                                            .GetString(Convert.FromBase64String(authHeaderValue.Parameter ?? string.Empty))
                                            .Split(':', 2);
                        if (credentials.Length == 2)
                        {
                            return IsAuthorized(credentials[0], credentials[1]);
                        }
                    }
                }

                return false;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        static bool IsAuthorized(string username, string password)
        {
            return GetEnvironmentVariable("AuthUser") == username && GetEnvironmentVariable("AuthPassword") == password;
        }

        public static string GetEnvironmentVariable(string name)
        {
            return System.Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
        }
    }
}
