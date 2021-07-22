using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Consumer;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace ConsumerCode
{
    public static class ServiceBus
    {
        public static async Task ConsumeAsync(Message sbMessage, IAsyncCollector<string> collector, ILogger log, string instanceId)
        {
            var timestamp = DateTime.UtcNow;
            log.LogTrace($@"[{sbMessage.UserProperties[@"TestRunId"]}]: Message received at {timestamp}: {JObject.FromObject(sbMessage)}");

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
                    { @"InstanceId", instanceId },
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
    }
}
