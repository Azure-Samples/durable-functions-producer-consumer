using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Consumer.ServiceBus
{
    public static class Functions
    {
        private static readonly string _instanceId = Guid.NewGuid().ToString();

        [FunctionName(nameof(ServiceBusQueueProcessorAsync))]
        public static async Task ServiceBusQueueProcessorAsync(
            [ServiceBusTrigger(@"%ServiceBusQueueName%", Connection = @"ServiceBusConnection", IsSessionsEnabled = true)] Message sbMessage,
            [EventHub(@"%CollectorEventHubName%", Connection = @"CollectorEventHubConnection")]IAsyncCollector<string> collector,
            ILogger log)
        {
            var timestamp = DateTime.UtcNow;
            log.LogTrace($@"[{sbMessage.UserProperties[@"TestRunId"]}]: Message received at {timestamp}: {JObject.FromObject(sbMessage).ToString()}");

            var enqueuedTime = sbMessage.ScheduledEnqueueTimeUtc;
            var elapsedTimeMs = (timestamp - enqueuedTime).TotalMilliseconds;

            if (sbMessage.UserProperties.TryGetValue(@"workTime", out var workTime))
            {
                await Task.Delay((int)workTime);
            }

            var collectorItem = new CollectorMessage
            {
                MessageProcessedTime = timestamp,
                TestRun = sbMessage.UserProperties[@"TestRunId"].ToString(),
                Trigger = @"ServiceBus",
                Properties = new Dictionary<string, object>
                {
                    { @"InstanceId", _instanceId },
                    { @"ExecutionId", Guid.NewGuid().ToString() },
                    { @"ElapsedTimeMs", elapsedTimeMs },
                    { @"ClientEnqueueTimeUtc", enqueuedTime },
                    { @"SystemEnqueuedTime", enqueuedTime },
                    { @"MessageId", sbMessage.MessageId },
                    { @"DequeuedTime", timestamp },
                    { @"Language", @"csharp" },
                }
            };

            await collector.AddAsync(collectorItem.ToString());

            log.LogMetric("messageProcessTimeMs",
                elapsedTimeMs,
                new Dictionary<string, object> {
                    { @"Session", sbMessage.SessionId },
                    { @"MessageNo", sbMessage.MessageId },
                    { @"EnqueuedTime", enqueuedTime },
                    { @"DequeuedTime", timestamp },
                    { @"Language", @"csharp" },
                });
        }

        [FunctionName(nameof(ClearDeadLetterServiceBusQueue))]
#pragma warning disable IDE0060 // Remove unused parameter
        public static void ClearDeadLetterServiceBusQueue([TimerTrigger("* 0 * * 1", RunOnStartup = true)]TimerInfo myTimer, ILogger log)
#pragma warning restore IDE0060 // Remove unused parameter
        {
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
