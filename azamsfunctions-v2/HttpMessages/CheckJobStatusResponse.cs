using System;

namespace azamsfunctions

{
    public class CheckJobStatusResponse
    {
        public int JobState { get; set; }       // The state of the job (int)

        public bool IsRunning { get; set; }      // True if job is running

        public bool IsSuccessful { get; set; }   // True is job is a success. Only valid if IsRunning = False

        public string ErrorText { get; set; }      // error(s) text if job state is error

        public DateTime? StartTime { get; set; }

        public DateTime? EndTime { get; set; }

        public string RunningDuration { get; set; }

        public ExtendedInfo ExtendedInfo { get; set; }
    }
}
