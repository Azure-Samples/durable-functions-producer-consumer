using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Consumer.net5.Extensions;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Consumer.EventHubs
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

        [Function(nameof(EventHubProcessorAsync))]
        [EventHubOutput(@"%CollectorEventHubName%", Connection = @"CollectorEventHubConnection")]
        public async Task<IEnumerable<string>> EventHubProcessorAsync(
            [EventHubTrigger(@"%EventHubName%", Connection = @"EventHubConnection")] string[] ehMessages,
            DateTime[] enqueuedTimeUtcArray,
            JsonElement[] propertiesArray,
            JsonElement partitionContext,
            FunctionContext context)
        {
#if DEBUG
            System.Diagnostics.Debugger.Launch();
#endif
            var log = context.GetLogger(nameof(EventHubProcessorAsync));

            var outputItems = new List<string>();

            for (int i = 0; i < ehMessages.Length; i++)
            {
                var ehMessage = ehMessages[i];
                log.LogInformation($@"EventHub Message received: {ehMessage}");


                var timestamp = DateTime.UtcNow;
                var enqueuedTime = enqueuedTimeUtcArray[i];
                var elapsedTimeMs = (timestamp - enqueuedTime).TotalMilliseconds;

                if (propertiesArray[i].TryGetProperty(@"workTime", out var value))
                {
                    await Task.Delay(value.GetInt32());
                }

                propertiesArray[i].TryGetProperty(@"TestRunId", out var testRunId);

                int messageId = propertiesArray[i].GetProperty(@"MessageId").GetInt32();
                var collectorItem = new CollectorMessage
                {
                    MessageProcessedTime = DateTime.UtcNow,
                    TestRun = testRunId.GetString(),
                    Trigger = @"EventHub",
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

                log.LogTrace($@"[{testRunId.GetString() ?? "null"}]: Message received at {timestamp}: {new
                {
                    Body = $@"{ehMessage.Length} byte(s)",
                    _elapsedTimeMs = elapsedTimeMs
                }}");

                log.LogMetric("messageProcessTimeMs",
                    elapsedTimeMs,
                    new Dictionary<string, object> {
                            { @"PartitionId", partitionContext.GetProperty("PartitionId").GetString() },
                            { @"MessageId", messageId.ToString() },
                            { @"SystemEnqueuedTime", enqueuedTime.ToString() },
                            { @"ClientEnqueuedTime", enqueuedTime.ToString() },
                            { @"DequeuedTime", timestamp.ToString() },
                            { @"Language", @"csharp" },
                    });

                _metricTelemetryClient.TrackMetric("messageProcessTimeMs",
                    elapsedTimeMs,
                    new Dictionary<string, string> {
                            { @"PartitionId", partitionContext.GetProperty("PartitionId").GetString() },
                            { @"MessageId", messageId.ToString() },
                            { @"SystemEnqueuedTime", enqueuedTime.ToString() },
                            { @"ClientEnqueuedTime", enqueuedTime.ToString() },
                            { @"DequeuedTime", timestamp.ToString() },
                            { @"Language", @"csharp" },
                    });

                outputItems.Add(collectorItem.ToString());
            }

            return outputItems;
        }
    }
}