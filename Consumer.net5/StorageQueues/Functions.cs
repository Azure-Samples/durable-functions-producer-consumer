using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Consumer.StorageQueues
{
    public class Functions
    {
        private static readonly string _instanceId = Guid.NewGuid().ToString();
        private readonly TelemetryClient _metricTelemetryClient;

        ///// Using dependency injection will guarantee that you use the same configuration for telemetry collected automatically and manually.
        public Functions(TelemetryConfiguration telemetryConfig)
        {

            _metricTelemetryClient = new TelemetryClient(telemetryConfig);
        }

        [Function(nameof(StorageQueueProcessorAsync))]
        [EventHubOutput(@"%CollectorEventHubName%", Connection = @"CollectorEventHubConnection")]
        public async Task<string> StorageQueueProcessorAsync(
            [QueueTrigger(@"%StorageQueueName%", Connection = @"StorageQueueConnection")] string queueMessage,
            DateTime insertionTime,
            int messageId,
            string testRunId,
            int? workTime,
            FunctionContext context)
        {
            var log = context.GetLogger(nameof(StorageQueueProcessorAsync));
            log.LogInformation($@"Storage Queue message received: {queueMessage}");

            var timestamp = DateTime.UtcNow;

            var parsedQueueMessage = JsonSerializer.Deserialize<JsonElement>(queueMessage);
            var enqueuedTime = parsedQueueMessage.GetProperty(@"EnqueueTimeUtc").GetDateTime();
            var elapsedTimeMs = (timestamp - enqueuedTime).TotalMilliseconds;

            if (workTime.HasValue)
            {
                await Task.Delay(workTime.Value);
            }

            var collectorItem = new CollectorMessage
            {
                MessageProcessedTime = DateTime.UtcNow,
                TestRun = testRunId,
                Trigger = @"Queue",
                Properties = new Dictionary<string, object>
                {
                    { @"InstanceId", _instanceId },
                    { @"ExecutionId", Guid.NewGuid().ToString() },
                    { @"ElapsedTimeMs", elapsedTimeMs },
                    { @"ClientEnqueueTimeUtc", enqueuedTime },
                    { @"SystemEnqueuedTime", insertionTime },
                    { @"MessageId", messageId },
                    { @"DequeuedTime", timestamp },
                    { @"Language", @"csharp" },
                }
            };

            log.LogTrace($@"[{testRunId}]: Message received at {timestamp}: {queueMessage}");

            _metricTelemetryClient.TrackMetric("messageProcessTimeMs",
                elapsedTimeMs,
                new Dictionary<string, string> {
                        { @"MessageId", messageId.ToString() },
                        { @"SystemEnqueuedTime", insertionTime.ToString() },
                        { @"ClientEnqueuedTime", enqueuedTime.ToString() },
                        { @"DequeuedTime", timestamp.ToString() },
                        { @"Language", @"csharp" },
                });

            return collectorItem.ToString();
        }
    }
}
