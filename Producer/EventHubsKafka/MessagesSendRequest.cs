using System;

namespace Producer.EventHubsKafka
{
    internal class MessagesSendRequest
    {
        public int MessageId { get; set; }
        public DateTime EnqueueTimeUtc { get; set; }
        public string TestRunId { get; set; }
        public int ConsumerWorkTime { get; set; }
        public string Message { get; set; }
    }
}