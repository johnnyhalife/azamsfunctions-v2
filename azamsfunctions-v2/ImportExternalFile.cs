using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Newtonsoft.Json.Linq;

namespace azamsfunctions
{
    public static class ImportExternalFile
    {
        [FunctionName("ImportExternalFile")]
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "import-external")]
            HttpRequestMessage req,
            TraceWriter log)
        {
            log.Info("C# HTTP trigger function processed a request.");

            var json = await req.Content.ReadAsStringAsync();

            if (string.IsNullOrEmpty(json))
            {
                return req.CreateResponse(
                    HttpStatusCode.BadRequest,
                    "The body parameter `source` is required");
            }

            dynamic body = JObject.Parse(json);

            if (body.source == null)
            {
                return req.CreateResponse(
                    HttpStatusCode.BadRequest,
                    "The body parameter `source` is required");
            }

            var storage = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("MediaStorageAccount"));
            var client = storage.CreateCloudBlobClient();

            var url = new Uri(body.source.ToString());
            var blobName = url.Segments[url.Segments.Length - 1];

            var container = client.GetContainerReference(Environment.GetEnvironmentVariable("BlobIngestContainer"));
            var blob = container.GetBlockBlobReference(blobName);

            await blob.StartCopyAsync(url);

            return req.CreateResponse(HttpStatusCode.OK);
        }
    }
}
