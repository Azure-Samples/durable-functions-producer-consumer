namespace Producer.ServiceBus
{
    internal class SessionCreateRequest
    {
        public string SessionId { get; set; }
        public int NumberOfMessagesPerSession { get; set; }
        public string TestRunId { get; set; }
        public int ConsumerWorkTime { get; set; }
    }
}