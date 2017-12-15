using System;
using Microsoft.WindowsAzure.MediaServices.Client;
using System.IO;


namespace azamsfunctions
{
    public class JobHelpers
    {
        public int AddTask(IJob job, IAsset sourceAsset, string value, string processor, string presetfilename, string stringtoreplace,
            ref int taskindex, CloudMediaContext _context, int priority = 10)
        {
            MediaServiceHelper helper = new MediaServiceHelper();
            if (value != null)
            {
                // Get a media processor reference, and pass to it the name of the 
                // processor to use for the specific task.
                IMediaProcessor mediaProcessor = helper.GetLatestMediaProcessorByName(processor, _context);

                string homePath = Environment.GetEnvironmentVariable("HOME", EnvironmentVariableTarget.Process);
                string presetPath;

                if (homePath == String.Empty)
                {
                    presetPath = @"../presets/" + presetfilename;
                }
                else
                {
                    presetPath = Path.Combine(homePath, @"site\repository\media-functions-for-logic-app\presets\" + presetfilename);
                }

                string Configuration = File.ReadAllText(presetPath).Replace(stringtoreplace, value);

                // Create a task with the encoding details, using a string preset.
                var task = job.Tasks.AddNew(processor + " task",
                   mediaProcessor,
                   Configuration,
                   TaskOptions.None);

                task.Priority = priority;

                // Specify the input asset to be indexed.
                task.InputAssets.Add(sourceAsset);

                // Add an output asset to contain the results of the job.
                task.OutputAssets.AddNew(sourceAsset.Name + " " + processor + " Output", AssetCreationOptions.None);

                return taskindex++;
            }
            else
            {
                return -1;
            }
        }

        public static string ReturnId(IJob job, int index)
        {
            return index > -1 ? job.OutputMediaAssets[index].Id : null;
        }

        public static string ReturnTaskId(IJob job, int index)
        {
            return index > -1 ? job.Tasks[index].Id : null;
        }

    }
}
