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

            var aggregatedMetadata = metadata.Select(m => new
            {
                AssetId = asset.Id,
                AssetAlternateId = asset.AlternateId,
                BaseStreamingUri = asset.GetSmoothStreamingUri(),

                m.Duration,
                AudioTracksCount = m.AudioTracks.Count(),
                VideoBitratesCount = metadata.Count(),
                Bitrate = m.VideoTracks.Max(vt => vt.Bitrate),
                Height = m.VideoTracks.Max(vt => vt.Height),
                Width = m.VideoTracks.Max(vt => vt.Width),
                AspectRatio = m.VideoTracks.Select(vt => $"{vt.DisplayAspectRatioNumerator}:{vt.DisplayAspectRatioDenominator}").FirstOrDefault()
            }).FirstOrDefault();

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
