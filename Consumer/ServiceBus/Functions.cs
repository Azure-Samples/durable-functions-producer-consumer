using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

#if NET5
using Microsoft.Azure.Functions.Worker;
#endif

namespace Consumer.ServiceBus
{
    public static class Functions
    {
        private static readonly string _instanceId = Guid.NewGuid().ToString();

        [FunctionName(nameof(ServiceBusQueueProcessorAsync))]
        public static async Task ServiceBusQueueProcessorAsync(
            [ServiceBusTrigger(@"%ServiceBusQueueName%", Connection = @"ServiceBusConnection", IsSessionsEnabled = true)] Message sbMessage,
            [EventHub(@"%CollectorEventHubName%", Connection = @"CollectorEventHubConnection")] IAsyncCollector<string> collector,
#if !NET5
            ILogger log)
        {
#else
            FunctionContext executionContext)
        {
            var log = executionContext.GetLogger(nameof(ServiceBusQueueProcessorAsync));
#endif
            await ConsumerCode.ServiceBus.ConsumeAsync(sbMessage, collector, log, _instanceId);
        }

        [FunctionName(nameof(ClearDeadLetterServiceBusQueue))]
#pragma warning disable IDE0060 // Remove unused parameter
        public static void ClearDeadLetterServiceBusQueue([TimerTrigger("* 0 * * 1", RunOnStartup = true)] TimerInfo myTimer,
#if !NET5
            ILogger log)
        {
#else
            FunctionContext executionContext)
        {
            var log = executionContext.GetLogger(nameof(ServiceBusQueueProcessorAsync));
#endif

#pragma warning restore IDE0060 // Remove unused parameter

            var deadLetterQueueName = $@"{Environment.GetEnvironmentVariable("ServiceBusQueueName")}/$DeadLetterQueue";
            var client = new QueueClient(Environment.GetEnvironmentVariable(@"ServiceBusConnection"),
                    deadLetterQueueName, ReceiveMode.PeekLock);
            client.RegisterMessageHandler((m, cancel) =>
            {
                try
                {
                    // swallow because the MessageHandlerOptions will autocomplete the msg for us
                    log.LogInformation($@"Cleared message {m.MessageId} from Dead Letter queue. Content: {Encoding.Default.GetString(m.Body)}");
                }
                catch (Exception completeEx)
                {
                    // log, but don't worry about, errors
                    log.LogError(completeEx, $@"Encountered an error completing msg in dead letter queue");
                }

                return Task.CompletedTask;
            }, new MessageHandlerOptions(exArgs =>
            {
                log.LogError(exArgs.Exception, $@"Encountered an error completing msg in dead letter queue");
                return Task.CompletedTask;
            })
            { AutoComplete = true });
        }
    }
}
