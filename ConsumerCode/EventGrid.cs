using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Consumer;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace ConsumerCode
{
    public static class EventGrid
    {
        public static async Task ConsumeAsync(EventGridEvent gridMessage, IAsyncCollector<string> collector, ILogger log, string instanceId)
        {
            var timestamp = DateTime.UtcNow;

            var jsonMessage = JObject.FromObject(gridMessage);
            var jsonContent = JObject.FromObject(gridMessage.Data);

            var enqueuedTime = gridMessage.EventTime;
            var elapsedTimeMs = (timestamp - enqueuedTime).TotalMilliseconds;

            if (jsonContent.TryGetValue(@"workTime", out var workTime))
            {
                await Task.Delay(workTime.Value<int>());
            }

            var collectorItem = new CollectorMessage
            {
                MessageProcessedTime = DateTime.UtcNow,
                TestRun = jsonContent.Value<string>(@"TestRunId"),
                Trigger = @"EventGrid",
                Properties = new Dictionary<string, object>
                {
                    { @"InstanceId", instanceId },
                    { @"ExecutionId", Guid.NewGuid().ToString() },
                    { @"ElapsedTimeMs", elapsedTimeMs },
                    { @"ClientEnqueueTimeUtc", enqueuedTime },
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
                        { @"ClientEnqueuedTime", enqueuedTime },
                        { @"DequeuedTime", timestamp },
                        { @"Language", @"csharp" },
                });
        }
    }
}
