using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Storage.Queues.Models;
using Consumer.net5.Extensions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Consumer.StorageQueues
{
    public static class Functions
    {
        private static readonly string _instanceId = Guid.NewGuid().ToString();

        [Function(nameof(StorageQueueProcessorAsync))]
        [EventHubOutput(@"%CollectorEventHubName%", Connection = @"CollectorEventHubConnection")]
        public static async Task<string> StorageQueueProcessorAsync(
            [QueueTrigger(@"%StorageQueueName%", Connection = @"StorageQueueConnection")] QueueMessage queueMessage,
            ILogger log)
        {
            var timestamp = DateTime.UtcNow;

            var jsonMessage = JObject.FromObject(queueMessage);
            var jsonContent = JObject.Parse(queueMessage.Body.ToString());

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
                    { @"InstanceId", _instanceId },
                    { @"ExecutionId", Guid.NewGuid().ToString() },
                    { @"ElapsedTimeMs", elapsedTimeMs },
                    { @"ClientEnqueueTimeUtc", enqueuedTime },
                    { @"SystemEnqueuedTime", queueMessage.InsertedOn },
                    { @"MessageId", jsonContent.Value<int>(@"MessageId") },
                    { @"DequeuedTime", timestamp },
                    { @"Language", @"csharp" },
                }
            };

            jsonMessage.Add(@"_elapsedTimeMs", elapsedTimeMs);
            log.LogTrace($@"[{jsonContent.Value<string>(@"TestRunId")}]: Message received at {timestamp}: {jsonMessage}");

            log.LogMetric("messageProcessTimeMs",
                elapsedTimeMs,
                new Dictionary<string, object> {
                        { @"MessageId", jsonContent.Value<int>(@"MessageId") },
                        { @"SystemEnqueuedTime", queueMessage.InsertedOn },
                        { @"ClientEnqueuedTime", enqueuedTime },
                        { @"DequeuedTime", timestamp },
                        { @"Language", @"csharp" },
                });

            return collectorItem.ToString();
        }
    }
}
