using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage;
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

            if (body.id == null)
            {
                return req.CreateResponse(
                    HttpStatusCode.BadRequest,
                    "The body parameter `id` is required");
            }

            var storage = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("MediaStorageAccount"));
            var client = storage.CreateCloudBlobClient();

            var url = new Uri(body.source.ToString());

            // If the URL uses an AWS Protocol let's create an AWS Signed URL
            // in order to perform the Cloud to Cloud copy without the need of 
            // downloading the asset from the Function.
            if (url.Scheme == "s3")
            {
                url = GetAmazonS3SignedURL(url);
            }

            var container = client.GetContainerReference(Environment.GetEnvironmentVariable("BlobIngestContainer"));
            await container.CreateIfNotExistsAsync();

            var blobName = $"{body.id.ToString()}-{Guid.NewGuid().ToString()}";
            var blob = container.GetBlockBlobReference(blobName);
            await blob.StartCopyAsync(url);

            return req.CreateResponse(HttpStatusCode.OK);
        }

        private static Uri GetAmazonS3SignedURL(Uri originalUrl)
        {
            var preSignedUrlRequest = new GetPreSignedUrlRequest()
            {
                BucketName = originalUrl.DnsSafeHost,
                Key = originalUrl.AbsolutePath.Substring(1),
                Expires = DateTime.Now.AddMinutes(30)
            };

            var accessKey = Environment.GetEnvironmentVariable("AwsAccessKeyId");
            var secretAccessKey = Environment.GetEnvironmentVariable("AwsSecretAccessKey");
            var regionEndpoint = Amazon.RegionEndpoint.GetBySystemName(Environment.GetEnvironmentVariable("AwsRegionEndpoint"));

            var client = new AmazonS3Client(accessKey, secretAccessKey, regionEndpoint);
            return new Uri(client.GetPreSignedURL(preSignedUrlRequest));
        }
    }
}
