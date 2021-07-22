using System;
using System.Threading.Tasks;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.EventHubs.Processor;
using Microsoft.Azure.WebJobs;

#if NET5
using Microsoft.Azure.Functions.Worker;
#else
using Microsoft.Extensions.Logging;
#endif

namespace Consumer.EventHubs
{
    public static class Functions
    {
        private static readonly string _instanceId = Guid.NewGuid().ToString();

        [FunctionName(nameof(EventHubProcessorAsync))]
        public static async Task EventHubProcessorAsync(
            [EventHubTrigger(@"%EventHubName%", Connection = @"EventHubConnection", ConsumerGroup = "%EventHubConsumerGroupName%")] EventData[] ehMessages,
            PartitionContext partitionContext,
            [EventHub(@"%CollectorEventHubName%", Connection = @"CollectorEventHubConnection")] IAsyncCollector<string> collector,
#if !NET5
            ILogger log)
        {
#else
            FunctionContext executionContext)
        {
            var log = executionContext.GetLogger(nameof(EventHubProcessorAsync));
#endif
            await ConsumerCode.EventHubs.ConsumeAsync(ehMessages, partitionContext, collector, log, _instanceId);
        }
    }
}
