using System;

namespace Producer.EventHubs
{
    internal class MessagesSendRequest
    {
        public int MessageId { get; set; }
        public DateTime EnqueueTimeUtc { get; set; }
        public string TestRunId { get; set; }
        public int ConsumerWorkTime { get; set; }
    }
}