using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.MediaServices.Client;
using Microsoft.WindowsAzure.Storage.Blob;

namespace azamsfunctions
{
    public static class EncodeAsset
    {
        [FunctionName("EncodeAsset")]
        public static async Task Run(
            [BlobTrigger("%BlobIngestContainer%/{name}", Connection = "MediaStorageAccount")]
            CloudBlockBlob blob,
            TraceWriter log)
        {
            log.Info($"C# Blob trigger function Processed blob Name: {blob.Name}");

            // We're avoiding incomplete blob processing. Eventually the engine 
            // will pick it up from the container, when it's completed.
            if (blob.CopyState.Status != CopyStatus.Success)
                return;

            var mediaStorageAccountCredentials = blob.ServiceClient.Credentials;
            var context = MediaContextHelper.CreateContext();

            var asset = await context.Assets.CreateFromBlobAsync(blob,
                mediaStorageAccountCredentials,
                AssetCreationOptions.None,
                CancellationToken.None);
            
            // Add the Alternate ID original stored on the Blob Name as partial path
            asset.AlternateId = blob.Name.Split('-').First();
            await asset.UpdateAsync();

            var mediaEncoderStandardTaskPreset = 
                Environment.GetEnvironmentVariable("MediaEncoderStandardTaskPreset");

            var job = context.Jobs.CreateWithSingleTask(
                MediaProcessorNames.MediaEncoderStandard,
                mediaEncoderStandardTaskPreset,
                asset, 
                $"{asset.Name} MES encoded", 
                AssetCreationOptions.None);

            var notificationName = Environment.GetEnvironmentVariable("QueueNotificationEndpointName");
            var queueName = Environment.GetEnvironmentVariable("EncodeJobsQueueName");

            var endpoint = await GetOrCreateQueueNotificationEndpoint(context, notificationName, queueName);
            job.JobNotificationSubscriptions.AddNew(NotificationJobState.All, endpoint);
            await job.SubmitAsync();

            // Clean up the original Blob from the Storage Account.
            await blob.DeleteIfExistsAsync();
        }

        public static async Task<INotificationEndPoint> GetOrCreateQueueNotificationEndpoint(
            MediaContextBase context, string name, string queueName)
        {
            var notificationEndpoint = context.NotificationEndPoints.Where(e => e.Name == name).FirstOrDefault();

            if (notificationEndpoint == null)
            {
                notificationEndpoint = await context.NotificationEndPoints.CreateAsync(
                    name, NotificationEndPointType.AzureQueue, queueName).ConfigureAwait(false);
            }

            return notificationEndpoint;
        }
    }
}
