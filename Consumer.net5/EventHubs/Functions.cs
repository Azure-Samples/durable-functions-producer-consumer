using System;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Consumer.EventHubs
{
    public static class Functions
    {
        private static readonly string _instanceId = Guid.NewGuid().ToString();

        [Function(nameof(EventHubProcessorAsync))]
        [EventHubOutput(@"%CollectorEventHubName%", Connection = @"CollectorEventHubConnection")]
        public static async Task<string> EventHubProcessorAsync(
            [EventHubTrigger(@"%EventHubName%", Connection = @"EventHubConnection", ConsumerGroup = "%EventHubConsumerGroupName%")] string[] ehMessages,
            //PartitionContext partitionContext,
            FunctionContext context)
        {
            var log = context.GetLogger(nameof(EventHubProcessorAsync));

            foreach (var ehMessage in ehMessages)
            {
                log.LogInformation($@"EventHub Message received: {ehMessage}");

                //// replace 'body' property so output isn't ridiculous
                //var jsonMessage = JObject.FromObject(ehMessage);

                //var timestamp = DateTime.UtcNow;
                //var enqueuedTime = (DateTime)ehMessage.Properties[@"EnqueueTimeUtc"];
                //var elapsedTimeMs = (timestamp - enqueuedTime).TotalMilliseconds;

                //if (ehMessage.Properties.TryGetValue(@"workTime", out var value))
                //{
                //    await Task.Delay((int)value);
                //}

                //ehMessage.Properties.TryGetValue(@"TestRunId", out var testRunId);

                //var collectorItem = new CollectorMessage
                //{
                //    MessageProcessedTime = DateTime.UtcNow,
                //    TestRun = testRunId?.ToString(),
                //    Trigger = @"EventHub",
                //    Properties = new Dictionary<string, object>
                //        {
                //            { @"InstanceId", _instanceId },
                //            { @"ExecutionId", Guid.NewGuid().ToString() },
                //            { @"ElapsedTimeMs", elapsedTimeMs },
                //            { @"ClientEnqueueTimeUtc", enqueuedTime },
                //            { @"MessageId", ehMessage.Properties[@"MessageId"] },
                //            { @"DequeuedTime", timestamp },
                //            { @"Language", @"csharp" },
                //        }
                //};

                //jsonMessage.Remove(@"Body");
                //jsonMessage.Add(@"Body", $@"{ehMessage.Body.Length} byte(s)");

                //jsonMessage.Add(@"_elapsedTimeMs", elapsedTimeMs);

                //log.LogTrace($@"[{testRunId?.ToString() ?? "null"}]: Message received at {timestamp}: {jsonMessage}");

                //log.LogMetric("messageProcessTimeMs",
                //    elapsedTimeMs,
                //    new Dictionary<string, object> {
                //            { @"PartitionId", partitionContext.PartitionId },
                //            { @"MessageId", ehMessage.Properties[@"MessageId"] },
                //            { @"SystemEnqueuedTime", enqueuedTime },
                //            { @"ClientEnqueuedTime", enqueuedTime },
                //            { @"DequeuedTime", timestamp },
                //            { @"Language", @"csharp" },
                //    });

                //return collectorItem.ToString();
            }

            return null;
        }
    }
}