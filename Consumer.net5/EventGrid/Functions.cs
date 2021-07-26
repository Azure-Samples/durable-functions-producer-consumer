using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Messaging.EventGrid;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Consumer.EventGrid
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

        [Function(nameof(EventGridProcessorAsync))]
        [EventHubOutput(@"%CollectorEventHubName%", Connection = @"CollectorEventHubConnection")]
        public async Task<string> EventGridProcessorAsync(
            [EventGridTrigger] string rawGridMessage,
            FunctionContext functionContext)
        {
#if DEBUG
            System.Diagnostics.Debugger.Launch();
#endif
            var log = functionContext.GetLogger(nameof(EventGridProcessorAsync));

            log.LogInformation($@"EventGrid message received: {rawGridMessage}");
            var gridMessage = JsonSerializer.Deserialize<EventGridEvent>(rawGridMessage);

            var timestamp = DateTime.UtcNow;

            var jsonContent = JsonSerializer.Deserialize<JsonElement>(gridMessage.Data);

            var enqueuedTime = gridMessage.EventTime;
            var elapsedTimeMs = (timestamp - enqueuedTime).TotalMilliseconds;

            if (jsonContent.TryGetProperty(@"workTime", out var workTime))
            {
                await Task.Delay(workTime.GetInt32());
            }

            var testRunId = jsonContent.GetProperty(@"TestRunId").GetString();
            var messageId = jsonContent.GetProperty(@"MessageId").GetInt32();
            var collectorItem = new CollectorMessage
            {
                MessageProcessedTime = DateTime.UtcNow,
                TestRun = testRunId,
                Trigger = @"EventGrid",
                Properties = new Dictionary<string, object>
                {
                    { @"InstanceId", _instanceId },
                    { @"ExecutionId", Guid.NewGuid().ToString() },
                    { @"ElapsedTimeMs", elapsedTimeMs },
                    { @"ClientEnqueueTimeUtc", enqueuedTime },
                    { @"MessageId", messageId },
                    { @"DequeuedTime", timestamp },
                    { @"Language", @"csharp" },
                }
            };

            log.LogTrace($@"[{testRunId}]: Message received at {timestamp}: {gridMessage.Data}");

            _metricTelemetryClient.TrackMetric("messageProcessTimeMs",
                elapsedTimeMs,
                new Dictionary<string, string> {
                        { @"MessageId", messageId.ToString() },
                        { @"ClientEnqueuedTime", enqueuedTime.ToString() },
                        { @"DequeuedTime", timestamp.ToString() },
                        { @"Language", @"csharp" },
                });

            return collectorItem.ToString();
        }
    }
}
