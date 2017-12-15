namespace azamsfunctions

{
    public class EncodeJobResponse
    {
        public string Id { get; set; }

        public string JobId { get; set; }

        public int OtherJobsQueue { get; set; }

        public Mes Mes { get; set; }

        public Mepw Mepw { get; set; }

        public IndexV1 IndexV1 { get; set; }

        public IndexV2 IndexV2 { get; set; }

        public Ocr Ocr { get; set; }

        public FaceDetection FaceDetection { get; set; }

        public FaceRedaction FaceRedaction { get; set; }

        public MotionDetection MotionDetection { get; set; }

        public Summarization Summarization { get; set; }

        public Hyperlapse Hyperlapse { get; set; }
    }
}
