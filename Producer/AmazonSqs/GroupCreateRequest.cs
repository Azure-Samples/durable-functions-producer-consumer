namespace Producer.AmazonSqs
{
    internal class GroupCreateRequest
    {
        public string GroupId { get; set; }
        public int NumberOfMessagesPerGroup { get; set; }
        public string TestRunId { get; set; }
        public int ConsumerWorkTime { get; set; }
    }
}