using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.MediaServices.Client;
using Newtonsoft.Json;

namespace azamsfunctions
{
    public static class CheckJobStatus
    {
        [FunctionName("CheckJobStatus")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = "check-job-status")]HttpRequestMessage req, TraceWriter log)
        {
            log.Info("C# HTTP trigger function processed a request.");

            var rawBody = await req.Content.ReadAsStringAsync();
            var data = JsonConvert.DeserializeObject<CheckJobStatusRequest>(rawBody);

            if (string.IsNullOrEmpty(data.JobId))
            {
                return req.CreateResponse(HttpStatusCode.BadRequest, new
                {
                    error = "Please pass the job ID in the input object (JobId)"
                });
            }

            // Read values from the environment/appsettings.
            string aadTenantDomain = Environment.GetEnvironmentVariable("AMSAADTenantDomain");
            string restAPIEndpoint = Environment.GetEnvironmentVariable("AMSRESTAPIEndpoint");
            string clientID = Environment.GetEnvironmentVariable("clientid");
            string clientSecret = Environment.GetEnvironmentVariable("clientsecret");

            IJob job;
            var stringBuilder = new StringBuilder();
            var context = default(CloudMediaContext);

            try
            {
                AzureAdTokenCredentials tokenCredentials = new AzureAdTokenCredentials(aadTenantDomain,
                                new AzureAdClientSymmetricKey(clientID, clientSecret),
                                AzureEnvironments.AzureCloudEnvironment);

                AzureAdTokenProvider tokenProvider = new AzureAdTokenProvider(tokenCredentials);

                context = new CloudMediaContext(new Uri(restAPIEndpoint), tokenProvider);
                job = context.Jobs.Where(j => j.Id == data.JobId).FirstOrDefault();

                if (job == null)
                {
                    return req.CreateResponse(HttpStatusCode.InternalServerError, new
                    {
                        error = "Job not found"
                    });
                }

                for (int i = 1; i <= 3; i++) // let's wait 3 times 5 seconds (15 seconds)
                {
                    if (job.State == JobState.Finished || job.State == JobState.Canceled || job.State == JobState.Error)
                    {
                        break;
                    }

                    // Wait 5 seconds
                    await Task.Delay(5 * 1000);

                    job = context.Jobs.Where(j => j.Id == job.Id).FirstOrDefault();
                }

                if (job.State == JobState.Error || job.State == JobState.Canceled)
                {
                    foreach (var taskenum in job.Tasks)
                    {
                        foreach (var details in taskenum.ErrorDetails)
                        {
                            stringBuilder.AppendLine(taskenum.Name + " : " + details.Message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return req.CreateResponse(HttpStatusCode.InternalServerError, new
                {
                    Error = ex.ToString()
                });
            }

            var extendedInfo = default(ExtendedInfo);

            if (data.ExtendedInfo && (job.State == JobState.Finished || job.State == JobState.Canceled || job.State == JobState.Error))
            {
                extendedInfo = new ExtendedInfo
                {
                    MediaUnitNumber = context.EncodingReservedUnits.FirstOrDefault().CurrentReservedUnits,
                    MediaUnitSize = MediaReservedUnitName(context.EncodingReservedUnits.FirstOrDefault().ReservedUnitType),
                    OtherJobsProcessing = context.Jobs.Where(j => j.State == JobState.Processing).Count(),
                    OtherJobsScheduled = context.Jobs.Where(j => j.State == JobState.Scheduled).Count(),
                    OtherJobsQueue = context.Jobs.Where(j => j.State == JobState.Queued).Count(),
                    AmsRESTAPIEndpoint = restAPIEndpoint,
                };
            }

            return req.CreateResponse(HttpStatusCode.OK, new
            {
                jobState = job.State,
                errorText = stringBuilder.ToString(),
                startTime = job.StartTime ?? null,
                endTime = job.EndTime ?? null,
                runningDuration = job.RunningDuration.ToString(),
                isRunning = !(job.State == JobState.Finished || job.State == JobState.Canceled || job.State == JobState.Error),
                isSuccessful = (job.State == JobState.Finished),
                ExtendedInfo = extendedInfo
            });
        }

        public static string MediaReservedUnitName(ReservedUnitType unitType)
        {
            switch (unitType)
            {
                case ReservedUnitType.Basic:
                default:
                    return "S1";

                case ReservedUnitType.Standard:
                    return "S2";

                case ReservedUnitType.Premium:
                    return "S3";
            }
        }
    }
}
