using System;

namespace Producer.ServiceBus
{
    internal class SessionMessagesCreateRequest
    {
        public string SessionId { get; set; }
        public int MessageId { get; set; }
        public DateTime EnqueueTimeUtc { get; set; }
        public string TestRunId { get; set; }
        public int ConsumerWorkTime { get; set; }
    }
}