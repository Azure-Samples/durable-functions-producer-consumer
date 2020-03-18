using System;

namespace Producer.EventHubsKafka
{
    internal class MessagesCreateRequest
    {
        public string TestRunId { get; set; }
        public int NumberOfMessagesPerPartition { get; set; }
        public int ConsumerWorkTime { get; set; }
    }
}