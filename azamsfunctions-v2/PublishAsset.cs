using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.MediaServices.Client;

namespace azamsfunctions
{
    public static class PublishAsset
    {
        [FunctionName("PublishAsset")]
        public static async Task Run(
            [QueueTrigger("%PublishJobsQueueName%", Connection = "MediaStorageAccount")]string assetId,
            TraceWriter log)
        {
            log.Info($"C# Queue trigger function processed: {assetId}");


            var context = MediaContextHelper.CreateContext();
            var asset = context.Assets.Where(a => a.Id == assetId).FirstOrDefault();

            if (asset == null)
                return;

            var locator = await context.Locators.CreateAsync(
                    LocatorType.OnDemandOrigin, 
                    asset, 
                    AccessPermissions.Read, 
                    TimeSpan.FromDays(365 * 10));

            log.Info($"Asset published to: {asset.GetSmoothStreamingUri().AbsoluteUri}");
        }
    }
}
