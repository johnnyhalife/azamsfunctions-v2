using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.MediaServices.Client;

namespace azamsfunctions
{
    public static class CheckJobStatus
    {
        [FunctionName("CheckJobStatus")]
        public static async Task Run(
            [QueueTrigger("%EncodeJobsQueueName%", Connection = "MediaStorageAccount")] dynamic queueItem,
            [Queue("%ContentProtectionJobsQueueName%", Connection = "MediaStorageAccount")] ICollector<string> outputDrmQueue,
            [Queue("%PublishJobsQueueName%", Connection = "MediaStorageAccount")] ICollector<string> outputPublishQueue,
            TraceWriter log)
        {
            log.Info($"C# Queue trigger function processed: {queueItem}");

            if (queueItem.Properties.NewState != JobState.Finished.ToString())
                return;
            
            var context = MediaContextHelper.CreateContext();
            var jobId = (string)queueItem.Properties.JobId;

            var job = context.Jobs.Where(j => j.Id == jobId).FirstOrDefault();

            if (job == null)
                return;

            // Force to publish current asset
            outputPublishQueue.Add(job.OutputMediaAssets.FirstOrDefault().Id);

            var mezzanineAsset = job.InputMediaAssets.FirstOrDefault();
            await mezzanineAsset.DeleteAsync();
        }
    }
}
