using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Consumer;
using Microsoft.Azure.Storage.Queue;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace ConsumerCode
{
    public static class StorageQueues
    {
        public static async Task ConsumeAsync(CloudQueueMessage queueMessage, IAsyncCollector<string> collector, ILogger log, string instanceId)
        {
            var timestamp = DateTime.UtcNow;

            var jsonMessage = JObject.FromObject(queueMessage);
            var jsonContent = JObject.Parse(queueMessage.AsString);

            var enqueuedTime = jsonContent.Value<DateTime>(@"EnqueueTimeUtc");
            var elapsedTimeMs = (timestamp - enqueuedTime).TotalMilliseconds;

            if (jsonContent.TryGetValue(@"workTime", out var workTime))
            {
                await Task.Delay(workTime.Value<int>());
            }

            var collectorItem = new CollectorMessage
            {
                MessageProcessedTime = DateTime.UtcNow,
                TestRun = jsonContent.Value<string>(@"TestRunId"),
                Trigger = @"Queue",
                Properties = new Dictionary<string, object>
                {
                    { @"InstanceId", instanceId },
                    { @"ExecutionId", Guid.NewGuid().ToString() },
                    { @"ElapsedTimeMs", elapsedTimeMs },
                    { @"ClientEnqueueTimeUtc", enqueuedTime },
                    { @"SystemEnqueuedTime", queueMessage.InsertionTime },
                    { @"MessageId", jsonContent.Value<int>(@"MessageId") },
                    { @"DequeuedTime", timestamp },
                    { @"Language", @"csharp" },
                }
            };

            await collector.AddAsync(collectorItem.ToString());

            jsonMessage.Add(@"_elapsedTimeMs", elapsedTimeMs);
            log.LogTrace($@"[{jsonContent.Value<string>(@"TestRunId")}]: Message received at {timestamp}: {jsonMessage}");

            log.LogMetric("messageProcessTimeMs",
                elapsedTimeMs,
                new Dictionary<string, object> {
                        { @"MessageId", jsonContent.Value<int>(@"MessageId") },
                        { @"SystemEnqueuedTime", queueMessage.InsertionTime},
                        { @"ClientEnqueuedTime", enqueuedTime },
                        { @"DequeuedTime", timestamp },
                        { @"Language", @"csharp" },
                });
        }

    }
}
