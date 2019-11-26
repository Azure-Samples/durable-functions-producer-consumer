namespace Producer.EventHubs
{
    internal class MessagesCreateRequest
    {
        public int NumberOfMessagesPerPartition { get; set; }
        public string TestRunId { get; set; }
        public int ConsumerWorkTime { get; set; }
    }
}