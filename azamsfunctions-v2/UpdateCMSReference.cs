using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.MediaServices.Client;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace azamsfunctions
{
    public static class UpdateCMSReference
    {
        [FunctionName("UpdateCMSReference")]
        public static async Task Run(
            [QueueTrigger("%UpdateCMSQueueName%", Connection = "MediaStorageAccount")]
            UpdateReferenceMessage message,
            TraceWriter log)
        {
            log.Info($"C# Queue trigger function processed: {message.AssetId}");

            var context = MediaContextHelper.CreateContext();
            var asset = context.Assets.Where(a => a.Id == message.AssetId).FirstOrDefault();

            // We're filtering out the Published options, however this would be a great
            // place to update with error status and implement and error control flow. 
            // e.g. Needs re-encoding and such.
            if (asset == null || message.Status != AssetWorkflowStatus.Published)
                return;

            // At this point you have the asset to do call the CMS and update it, all the required 
            // information for the CMS, will be read from the Asset Metadata as follows.
            var metadata = await asset.GetMetadataAsync(CancellationToken.None);

            // Sometimes by the time we reach this point the locator ain't ready. Hence we're 
            // we're throwing an exception when metadata isn't ready, to reprocess on a 
            // future iteration. 
            if (metadata == null || metadata.Count() == 0)
                throw new Exception($"Asset {asset.Id} metadata is not ready yet.");

            var aggregatedMetadata = new
            {
                AssetId = asset.Id,
                AssetAlternateId = asset.AlternateId,
                BaseStreamingUri = asset.GetSmoothStreamingUri(),

                Duration = metadata.Max(m => m.Duration),
                AudioTracksCount = metadata.Max(m => m.AudioTracks.Count()),
                VideoBitratesCount = metadata.Count(),
                Bitrate = metadata.SelectMany(m => m.VideoTracks).Max(vt => vt.Bitrate),
                Height = metadata.SelectMany(m => m.VideoTracks).Max(vt => vt.Height),
                Width = metadata.SelectMany(m => m.VideoTracks).Max(vt => vt.Width),
                AspectRatio = metadata.SelectMany(m => m.VideoTracks).Select(vt => $"{vt.DisplayAspectRatioNumerator}:{vt.DisplayAspectRatioDenominator}").FirstOrDefault()
            };

            log.Info($"AssetId: {aggregatedMetadata.AssetId}");
            log.Info($"AssetAlternateId: {aggregatedMetadata.AssetAlternateId}");
            log.Info($"Base Streaming URL: {asset.GetSmoothStreamingUri()}");
            log.Info($"Duration: {aggregatedMetadata.Duration}");
            log.Info($"Number of Audio Tracks: {aggregatedMetadata.AudioTracksCount}");
            log.Info($"Number of Video Bitrates: {aggregatedMetadata.VideoBitratesCount}");
            log.Info($"Bitrate: {aggregatedMetadata.Bitrate}");
            log.Info($"Height: {aggregatedMetadata.Height}");
            log.Info($"Width: {aggregatedMetadata.Width}");
            log.Info($"AspectRatio: {aggregatedMetadata.AspectRatio}");
            
            using (var client = new HttpClient())
            {
                await client.PostAsJsonAsync(
                    Environment.GetEnvironmentVariable("CMSCallbackUrl"), aggregatedMetadata);
            }
        }
    }
}
