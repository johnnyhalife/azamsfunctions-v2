using Microsoft.WindowsAzure.MediaServices.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace azamsfunctions
{
    static class MediaContextHelper
    {
        public static MediaContextBase CreateContext()
        {
            string aadTenantDomain = Environment.GetEnvironmentVariable("AMSAADTenantDomain");
            string restAPIEndpoint = Environment.GetEnvironmentVariable("AMSRESTAPIEndpoint");
            string clientID = Environment.GetEnvironmentVariable("AMSRESTAPIClientId");
            string clientSecret = Environment.GetEnvironmentVariable("AMSRESTAPIClientSecret");

            AzureAdTokenCredentials tokenCredentials = new AzureAdTokenCredentials(aadTenantDomain,
                                new AzureAdClientSymmetricKey(clientID, clientSecret),
                                AzureEnvironments.AzureCloudEnvironment);

            AzureAdTokenProvider tokenProvider = new AzureAdTokenProvider(tokenCredentials);

            return new CloudMediaContext(new Uri(restAPIEndpoint), tokenProvider);
        }
    }
}
