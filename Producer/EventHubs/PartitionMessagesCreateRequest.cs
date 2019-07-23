using System;

namespace Producer.EventHubs
{
    internal class PartitionMessagesCreateRequest
    {
        public string PartitionId { get; set; }
        public int MessageId { get; set; }
        public DateTime EnqueueTimeUtc { get; set; }
        public string TestRunId { get; set; }
        public int ConsumerWorkTime { get; set; }
    }
}