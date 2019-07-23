using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Consumer.EventHubs
{
    public static class Functions
    {
        private static readonly string _instanceId = Guid.NewGuid().ToString();

        [FunctionName(nameof(EventHubProcessorAsync))]
        public static async System.Threading.Tasks.Task EventHubProcessorAsync(
            [EventHubTrigger(@"%EventHubName%", Connection = @"EventHubConnection")] EventData[] ehMessages,
            [EventHub(@"%CollectorEventHubName%", Connection = @"CollectorEventHubConnection")]IAsyncCollector<string> collector,
            ILogger log)
        {
            foreach (var ehMessage in ehMessages)
            {
                // replace 'body' property so output isn't ridiculous
                var jsonMessage = JObject.FromObject(ehMessage);

                var timestamp = DateTime.UtcNow;
                var enqueuedTime = (DateTime)ehMessage.Properties[@"EnqueueTimeUtc"];
                var elapsedTimeMs = (timestamp - enqueuedTime).TotalMilliseconds;

                if (ehMessage.Properties.TryGetValue(@"workTime", out var value))
                {
                    await Task.Delay((int)value);
                }

                var collectorItem = new CollectorMessage
                {
                    MessageProcessedTime = DateTime.UtcNow,
                    TestRun = ehMessage.Properties[@"TestRunId"].ToString(),
                    Trigger = @"EventHub",
                    Properties = new Dictionary<string, object>
                    {
                        { @"InstanceId", _instanceId },
                        { @"ExecutionId", Guid.NewGuid().ToString() },
                        { @"ElapsedTimeMs", elapsedTimeMs },
                        { @"ClientEnqueueTimeUtc", enqueuedTime },
                        { @"MessageId", ehMessage.Properties[@"MessageId"] },
                        { @"DequeuedTime", timestamp },
                        { @"Language", @"csharp" },
                    }
                };

                await collector.AddAsync(collectorItem.ToString());

                jsonMessage.Remove(@"Body");
                jsonMessage.Add(@"Body", $@"{ehMessage.Body.Count} byte(s)");

                jsonMessage.Add(@"_elapsedTimeMs", elapsedTimeMs);

                log.LogTrace($@"[{ehMessage.Properties[@"TestRunId"]}]: Message received at {timestamp}: {jsonMessage.ToString()}");

                log.LogMetric("messageProcessTimeMs",
                    elapsedTimeMs,
                    new Dictionary<string, object> {
                        { @"PartitionId", ehMessage.Properties[@"PartitionId"] },
                        { @"MessageId", ehMessage.Properties[@"MessageId"] },
                        { @"SystemEnqueuedTime", enqueuedTime },
                        { @"ClientEnqueuedTime", enqueuedTime },
                        { @"DequeuedTime", timestamp },
                        { @"Language", @"csharp" },
                    });
            }
        }
    }
}
