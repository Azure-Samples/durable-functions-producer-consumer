namespace Producer.EventHubs
{
    internal class PartitionCreateRequest
    {
        public string PartitionId { get; set; }
        public int NumberOfMessagesPerPartition { get; set; }
        public string TestRunId { get; set; }
        public int ConsumerWorkTime { get; set; }
    }
}