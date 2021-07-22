using System;
using System.Threading.Tasks;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;

#if NET5
using Microsoft.Azure.Functions.Worker;
#else
using Microsoft.Extensions.Logging;
#endif

namespace Consumer.EventGrid
{
    public static class Functions
    {
        private static readonly string _instanceId = Guid.NewGuid().ToString();

        [FunctionName(nameof(EventGridProcessorAsync))]
        public static async Task EventGridProcessorAsync(
            [EventGridTrigger] EventGridEvent gridMessage,
            [EventHub(@"%CollectorEventHubName%", Connection = @"CollectorEventHubConnection")] IAsyncCollector<string> collector,
#if !NET5
            ILogger log)
        {
#else
            FunctionContext executionContext)
        {
            var log = executionContext.GetLogger(nameof(EventGridProcessorAsync));
#endif
            await ConsumerCode.EventGrid.ConsumeAsync(gridMessage, collector, log, _instanceId);
        }
    }
}
