using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Consumer.ServiceBus
{
    public partial class Functions
    {
        private static readonly string _instanceId = Guid.NewGuid().ToString();
        private readonly TelemetryClient _metricTelemetryClient;

        ///// Using dependency injection will guarantee that you use the same configuration for telemetry collected automatically and manually.
        public Functions(TelemetryConfiguration telemetryConfig)
        {

            _metricTelemetryClient = new TelemetryClient(telemetryConfig);
        }

        [Function(nameof(ServiceBusQueueProcessorAsync))]
        [EventHubOutput(@"%CollectorEventHubName%", Connection = @"CollectorEventHubConnection")]
        public async Task<string> ServiceBusQueueProcessorAsync(
            [ServiceBusTrigger(@"%ServiceBusQueueName%", Connection = @"ServiceBusConnection", IsSessionsEnabled = true)] string sbMessage,
            string messageId,
            DateTime enqueuedTimeUtc,
            JsonElement userProperties,
            JsonElement messageSession,
            FunctionContext context)
        {
#if DEBUG
            System.Diagnostics.Debugger.Launch();
#endif
            var log = context.GetLogger(nameof(ServiceBusQueueProcessorAsync));
            log.LogInformation($@"ServiceBus message received: {sbMessage}");
            var timestamp = DateTime.UtcNow;
            string testRunId = userProperties.GetProperty(@"TestRunId").GetString();

            log.LogTrace($@"[{testRunId}]: Message received at {timestamp}: {sbMessage}");

            var enqueuedTime = enqueuedTimeUtc;
            var elapsedTimeMs = (timestamp - enqueuedTime).TotalMilliseconds;

            if (userProperties.TryGetProperty(@"workTime", out var workTimeProperty))
            {
                await Task.Delay((int)workTimeProperty.GetInt32());
            }

            var collectorItem = new CollectorMessage
            {
                MessageProcessedTime = timestamp,
                TestRun = testRunId,
                Trigger = @"ServiceBus",
                Properties = new Dictionary<string, object>
                {
                    { @"InstanceId", _instanceId },
                    { @"ExecutionId", Guid.NewGuid().ToString() },
                    { @"ElapsedTimeMs", elapsedTimeMs },
                    { @"ClientEnqueueTimeUtc", enqueuedTime },
                    { @"SystemEnqueuedTime", enqueuedTime },
                    { @"MessageId", messageId },
                    { @"DequeuedTime", timestamp },
                    { @"Language", @"csharp" },
                }
            };

            _metricTelemetryClient.TrackMetric("messageProcessTimeMs",
                elapsedTimeMs,
                new Dictionary<string, string> {
                    { @"Session", messageSession.GetProperty(@"SessionId").GetString() },
                    { @"MessageNo", messageId },
                    { @"EnqueuedTime", enqueuedTime.ToString() },
                    { @"DequeuedTime", timestamp.ToString() },
                    { @"Language", @"csharp" },
                });

            return collectorItem.ToString();
        }

        [Function(nameof(ClearDeadLetterServiceBusQueue))]
#pragma warning disable IDE0060 // Remove unused parameter
        public static async Task ClearDeadLetterServiceBusQueue([TimerTrigger("* 0 * * 1", RunOnStartup = true)] TimerInfo myTimer,
            ILogger log)
        {
#pragma warning restore IDE0060 // Remove unused parameter

            var deadLetterQueueName = $@"{Environment.GetEnvironmentVariable("ServiceBusQueueName")}/$DeadLetterQueue";
            await using var client = new Azure.Messaging.ServiceBus.ServiceBusClient(Environment.GetEnvironmentVariable(@"ServiceBusConnection"));
            client.ConfigureAwait(false);

            await using var processor = client.CreateProcessor(deadLetterQueueName, new Azure.Messaging.ServiceBus.ServiceBusProcessorOptions { AutoCompleteMessages = true });
            processor.ProcessMessageAsync += a =>
             {
                 try
                 {
                     // swallow because the MessageHandlerOptions will autocomplete the msg for us
                     log.LogInformation($@"Cleared message {a.Message.MessageId} from Dead Letter queue. Content: {Encoding.Default.GetString(a.Message.Body)}");
                 }
                 catch (Exception completeEx)
                 {
                     // log, but don't worry about, errors
                     log.LogError(completeEx, $@"Encountered an error completing msg in dead letter queue");
                 }
                 return Task.CompletedTask;
             };

            processor.ProcessErrorAsync += a =>
            {
                log.LogError(a.Exception, $@"Encountered an error completing msg in dead letter queue");
                return Task.CompletedTask;
            };
        }
    }
}
