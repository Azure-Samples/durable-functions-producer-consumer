using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.Storage.Queue;

#if NET5
using Microsoft.Azure.Functions.Worker;
#else
using Microsoft.Extensions.Logging;
#endif

namespace Consumer.StorageQueues
{
    public static class Functions
    {
        private static readonly string _instanceId = Guid.NewGuid().ToString();

        [FunctionName(nameof(StorageQueueProcessorAsync))]
        public static async System.Threading.Tasks.Task StorageQueueProcessorAsync(
            [QueueTrigger(@"%StorageQueueName%", Connection = @"StorageQueueConnection")] CloudQueueMessage queueMessage,
            [EventHub(@"%CollectorEventHubName%", Connection = @"CollectorEventHubConnection")]IAsyncCollector<string> collector,
#if !NET5
            ILogger log)
        {
#else
            FunctionContext executionContext)
        {
            var log = executionContext.GetLogger(nameof(StorageQueueProcessorAsync));
#endif
            await ConsumerCode.StorageQueues.ConsumeAsync(queueMessage, collector, log, _instanceId);
        }
    }
}
