using System;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Consumer.EventGrid
{
    public static class Functions
    {
        private static readonly string _instanceId = Guid.NewGuid().ToString();

        [Function(nameof(EventGridProcessorAsync))]
        [EventHubOutput(@"%CollectorEventHubName%", Connection = @"CollectorEventHubConnection")]
        public static async Task<string> EventGridProcessorAsync(
            [EventGridTrigger] string gridMessage,
            FunctionContext functionContext)
        {
            var log = functionContext.GetLogger(nameof(EventGridProcessorAsync));
            log.LogInformation($@"EventGrid message received: {gridMessage}");
            //log.LogInformation($@"EventGrid Message received: {JsonSerializer.Serialize(gridMessage)}");

            //var timestamp = DateTime.UtcNow;

            //var jsonMessage = JObject.FromObject(gridMessage);
            //var jsonContent = JObject.FromObject(gridMessage.Data);

            //var enqueuedTime = gridMessage.EventTime;
            //var elapsedTimeMs = (timestamp - enqueuedTime).TotalMilliseconds;

            //if (jsonContent.TryGetValue(@"workTime", out var workTime))
            //{
            //    await Task.Delay(workTime.Value<int>());
            //}

            //var collectorItem = new CollectorMessage
            //{
            //    MessageProcessedTime = DateTime.UtcNow,
            //    TestRun = jsonContent.Value<string>(@"TestRunId"),
            //    Trigger = @"EventGrid",
            //    Properties = new Dictionary<string, object>
            //    {
            //        { @"InstanceId", _instanceId },
            //        { @"ExecutionId", Guid.NewGuid().ToString() },
            //        { @"ElapsedTimeMs", elapsedTimeMs },
            //        { @"ClientEnqueueTimeUtc", enqueuedTime },
            //        { @"MessageId", jsonContent.Value<int>(@"MessageId") },
            //        { @"DequeuedTime", timestamp },
            //        { @"Language", @"csharp" },
            //    }
            //};

            //jsonMessage.Add(@"_elapsedTimeMs", elapsedTimeMs);
            //log.LogTrace($@"[{jsonContent.Value<string>(@"TestRunId")}]: Message received at {timestamp}: {jsonMessage}");

            //log.LogMetric("messageProcessTimeMs",
            //    elapsedTimeMs,
            //    new Dictionary<string, object> {
            //            { @"MessageId", jsonContent.Value<int>(@"MessageId") },
            //            { @"ClientEnqueuedTime", enqueuedTime },
            //            { @"DequeuedTime", timestamp },
            //            { @"Language", @"csharp" },
            //    });

            return null;// collectorItem.ToString();
        }
    }
}
