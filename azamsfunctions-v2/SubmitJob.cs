using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.MediaServices.Client;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace azamsfunctions
{
    public static class SubmitJob
    {
        [FunctionName("SubmitJob")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = "submit-job")]HttpRequestMessage req, TraceWriter log)
        {
            log.Info("C# HTTP trigger function processed a request.");

            var rawBody = await req.Content.ReadAsStringAsync();
            var data = JsonConvert.DeserializeObject<EncodeJobRequest>(rawBody);

            // AssetId should not be null
            if (string.IsNullOrEmpty(data.AssetId))
            {
                return req.CreateResponse(HttpStatusCode.BadRequest, new
                {
                    error = "Please pass asset ID in the input object (assetId)"
                });
            }

            // Read values from the environment/appsettings.
            string aadTenantDomain = Environment.GetEnvironmentVariable("AMSAADTenantDomain");
            string restAPIEndpoint = Environment.GetEnvironmentVariable("AMSRESTAPIEndpoint");
            string clientID = Environment.GetEnvironmentVariable("clientid");
            string clientSecret = Environment.GetEnvironmentVariable("clientsecret");

            var mediaServiceHelper = new MediaServiceHelper();

            int taskindex = 0;
            int outputMES = -1;
            int outputMEPW = -1;
            int outputIndex1 = -1;
            int outputIndex2 = -1;
            int outputOCR = -1;
            int outputFaceDetection = -1;
            int outputMotion = -1;
            int outputSummarization = -1;
            int outputHyperlapse = -1;
            int outputFaceRedaction = -1;
            int numberJobsQueue = 0;

            IJob job = null;
            IAsset outputEncoding = null;

            try
            {
                AzureAdTokenCredentials tokenCredentials = new AzureAdTokenCredentials(aadTenantDomain,
                                new AzureAdClientSymmetricKey(clientID, clientSecret),
                                AzureEnvironments.AzureCloudEnvironment);

                AzureAdTokenProvider tokenProvider = new AzureAdTokenProvider(tokenCredentials);

                var context = new CloudMediaContext(new Uri(restAPIEndpoint), tokenProvider);

                // Find the asset
                IAsset asset = context.Assets.Where(a => a.Id == data.AssetId).FirstOrDefault();

                if (asset == null)
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest, new
                    {
                        error = "Asset not found"
                    });
                }

                // User wants to use encoder output for media analytics
                // Leaving this commented out as couldn't find reference to useEncoderOutputForAnalytics
                // if (data.useEncoderOutputForAnalytics != null && (data.mesPreset != null || data.mesPreset != null))  
                // {
                //  useEncoderOutputForAnalytics = (bool)data.useEncoderOutputForAnalytics;
                // }

                // Should add priority? jobName? to the request object?
                job = context.Jobs.Create("Azure Functions Job", 10);

                if (!string.IsNullOrEmpty(data.MesPreset))
                {
                    IMediaProcessor processorMES = mediaServiceHelper.GetLatestMediaProcessorByName("Media Encoder Standard", context);
                    string mesPreset = data.MesPreset;

                    if (data.MesPreset.ToUpper().EndsWith(".JSON"))
                    {
                        var presetPath = @"presets\" + data.MesPreset;
                        mesPreset = File.ReadAllText(presetPath);
                    }

                    // Create a task with the encoding details, using a string preset.
                    // In this case "H264 Multiple Bitrate 720p" system defined preset is used.
                    ITask taskEncoding = job.Tasks.AddNew("MES encoding task",
                       processorMES,
                       mesPreset,
                       TaskOptions.None);

                    // Specify the input asset to be encoded.
                    taskEncoding.InputAssets.Add(asset);
                    outputMES = taskindex++;

                    // Add an output asset to contain the results of the job. 
                    // This output is specified as AssetCreationOptions.None, which means the output asset is not encrypted. 
                    outputEncoding = taskEncoding.OutputAssets.AddNew(asset.Name + " MES encoded", AssetCreationOptions.None);
                }

                // Should we add data.workflowAssetId to the request object? (skipped that logic as object ain't present)
                var jobsHelper = new JobHelpers();

                // indexV1Language, indexV2Language, ocrLanguage, faceDetectionMode, faceRedactionMode, motionDetectionLevel
                // summarizationDuration, hyperlapseSpeed should be added to the request object.
                //outputIndex1 = jobsHelper.AddTask(job, asset, string.Empty, "Azure Media Indexer", "IndexerV1.xml", "English", ref taskindex, context);
                //outputIndex2 = jobsHelper.AddTask(job, asset, string.Empty, "Azure Media Indexer 2 Preview", "IndexerV2.json", "EnUs", ref taskindex, context);
                //outputOCR = jobsHelper.AddTask(job, asset, string.Empty, "Azure Media OCR", "OCR.json", "AutoDetect", ref taskindex, context);
                //outputFaceDetection = jobsHelper.AddTask(job, asset, string.Empty, "Azure Media Face Detector", "FaceDetection.json", "PerFaceEmotion", ref taskindex, context);
                //outputFaceRedaction = jobsHelper.AddTask(job, asset, string.Empty, "Azure Media Redactor", "FaceRedaction.json", "combined", ref taskindex, context);
                //outputMotion = jobsHelper.AddTask(job, asset, string.Empty, "Azure Media Motion Detector", "MotionDetection.json", "medium", ref taskindex, context);
                //outputSummarization = jobsHelper.AddTask(job, asset, string.Empty, "Azure Media Video Thumbnails", "Summarization.json", "0.0", ref taskindex, context);
                //outputHyperlapse = jobsHelper.AddTask(job, asset, string.Empty, "Azure Media Hyperlapse", "Hyperlapse.json", "8", ref taskindex, context);

                job.Submit();

                numberJobsQueue = context.Jobs.Where(j => j.State == JobState.Queued).Count();
            }
            catch (Exception ex)
            {
                return req.CreateResponse(HttpStatusCode.InternalServerError, new
                {
                    Error = ex.ToString()
                });
            }

            return req.CreateResponse(HttpStatusCode.OK, new EncodeJobResponse
            {
                JobId = job.Id,
                OtherJobsQueue = numberJobsQueue,
                Mes = new Mes
                {
                    AssetId = JobHelpers.ReturnId(job, outputMES),
                    TaskId = JobHelpers.ReturnTaskId(job, outputMES)
                },
                Mepw = new Mepw
                {
                    AssetId = JobHelpers.ReturnId(job, outputMEPW),
                    TaskId = JobHelpers.ReturnTaskId(job, outputMEPW)
                },
                IndexV1 = new IndexV1
                {
                    AssetId = JobHelpers.ReturnId(job, outputIndex1),
                    TaskId = JobHelpers.ReturnTaskId(job, outputIndex1),
                },
                IndexV2 = new IndexV2
                {
                    AssetId = JobHelpers.ReturnId(job, outputIndex2),
                    TaskId = JobHelpers.ReturnTaskId(job, outputIndex2),
                },
                Ocr = new Ocr
                {
                    AssetId = JobHelpers.ReturnId(job, outputOCR),
                    TaskId = JobHelpers.ReturnTaskId(job, outputOCR)
                },
                FaceDetection = new FaceDetection
                {
                    AssetId = JobHelpers.ReturnId(job, outputFaceDetection),
                    TaskId = JobHelpers.ReturnTaskId(job, outputFaceDetection)
                },
                FaceRedaction = new FaceRedaction
                {
                    AssetId = JobHelpers.ReturnId(job, outputFaceRedaction),
                    TaskId = JobHelpers.ReturnTaskId(job, outputFaceRedaction)
                },
                MotionDetection = new MotionDetection
                {
                    AssetId = JobHelpers.ReturnId(job, outputMotion),
                    TaskId = JobHelpers.ReturnTaskId(job, outputMotion)
                },
                Summarization = new Summarization
                {
                    AssetId = JobHelpers.ReturnId(job, outputSummarization),
                    TaskId = JobHelpers.ReturnTaskId(job, outputSummarization)
                },
                Hyperlapse = new Hyperlapse
                {
                    AssetId = JobHelpers.ReturnId(job, outputHyperlapse),
                    TaskId = JobHelpers.ReturnTaskId(job, outputHyperlapse)
                }
            });
        }
    }
}
