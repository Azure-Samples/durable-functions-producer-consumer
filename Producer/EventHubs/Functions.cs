using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Producer.EventHubs
{
    public static class Functions
    {
        [FunctionName(nameof(PostToEventHub))]
        public static async Task<IActionResult> PostToEventHub(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest request,
            [DurableClient] IDurableOrchestrationClient client,
            ILogger log)
        {
            var inputObject = JObject.Parse(await request.ReadAsStringAsync());
            var numberOfMessagesPerPartition = inputObject.Value<int>(@"NumberOfMessagesPerPartition");
            var numberOfPartitions = Convert.ToInt32(Environment.GetEnvironmentVariable("EventHubPartitions"));

            var workTime = -1;
            if (inputObject.TryGetValue(@"WorkTime", out var workTimeVal))
            {
                workTime = workTimeVal.Value<int>();
            }

            var orchestrationIds = new List<string>();
            var testRunId = Guid.NewGuid().ToString();
            for (var c = 1; c <= numberOfPartitions; c++)
            {
                var partitionKey = Guid.NewGuid().ToString();
                var orchId = await client.StartNewAsync(nameof(GenerateMessagesForEventHub),
                    new MessagesCreateRequest
                    {
                        TestRunId = testRunId,
                        NumberOfMessagesPerPartition = numberOfMessagesPerPartition,
                        ConsumerWorkTime = workTime,
                    });

                log.LogTrace($@"Kicked off message creation for session {c}...");

                orchestrationIds.Add(orchId);
            }

            return await client.WaitForCompletionOrCreateCheckStatusResponseAsync(request, orchestrationIds.First(), TimeSpan.FromMinutes(2));
        }

        [FunctionName(nameof(GenerateMessagesForEventHub))]
        public static async Task<JObject> GenerateMessagesForEventHub(
            [OrchestrationTrigger] IDurableOrchestrationContext ctx,
            ILogger log)
        {
            var req = ctx.GetInput<MessagesCreateRequest>();

            var messages = Enumerable.Range(1, req.NumberOfMessagesPerPartition)
                    .Select(m =>
                    {
                        var enqueueTime = ctx.CurrentUtcDateTime;
                        return new MessagesSendRequest
                        {
                            MessageId = m,
                            EnqueueTimeUtc = enqueueTime,
                            TestRunId = req.TestRunId,
                            ConsumerWorkTime = req.ConsumerWorkTime,
                        };
                    }).ToList();

            try
            {
                return await ctx.CallActivityAsync<bool>(nameof(PostMessagesToEventHub), messages)
                    ? JObject.FromObject(new { req.TestRunId })
                    : JObject.FromObject(new { Error = $@"An error occurred executing orchestration {ctx.InstanceId}" });
            }
            catch (Exception ex)
            {
                log.LogError(ex, @"An error occurred queuing message generation to Event Hub");
                return JObject.FromObject(new { Error = $@"An error occurred executing orchestration {ctx.InstanceId}: {ex}" });
            }
        }

        private const int MAX_RETRY_ATTEMPTS = 10;
        private static readonly Lazy<string> _messageContent = new Lazy<string>(() =>
        {
            using var sr = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream($@"Producer.messagecontent.txt"));
            return sr.ReadToEnd();
        });

        [FunctionName(nameof(PostMessagesToEventHub))]
        public static async Task<bool> PostMessagesToEventHub([ActivityTrigger] IDurableActivityContext ctx,
            [EventHub("%EventHubName%", Connection = @"EventHubConnection")] IAsyncCollector<EventData> queueMessages,
            ILogger log)
        {
            var messages = ctx.GetInput<IEnumerable<MessagesSendRequest>>();

            foreach (var messageToPost in messages.Select(m =>
                {
                    var r = new EventData(Encoding.Default.GetBytes(_messageContent.Value));
                    r.Properties.Add(@"MessageId", m.MessageId);
                    r.Properties.Add(@"EnqueueTimeUtc", m.EnqueueTimeUtc);
                    r.Properties.Add(@"TestRunId", m.TestRunId);

                    if (m.ConsumerWorkTime > 0)
                    {
                        r.Properties.Add(@"workTime", m.ConsumerWorkTime);
                    }

                    return r;
                }
            ))
            {
                var retryCount = 0;
                var retry = false;
                do
                {
                    retryCount++;
                    try
                    {
                        await queueMessages.AddAsync(messageToPost);
                        retry = false;
                    }
                    catch (Exception ex)
                    {
                        log.LogError(ex, $@"Error posting message with TestRunID '{messageToPost.Properties[@"TestRunId"]}' and MessageId '{messageToPost.Properties[@"MessageId"]}'. Retrying...");
                        retry = true;
                    }

                    if (retry && retryCount >= MAX_RETRY_ATTEMPTS)
                    {
                        log.LogError($@"Unable to post message with TestRunID '{messageToPost.Properties[@"TestRunId"]}' and MessageId '{messageToPost.Properties[@"MessageId"]}' after {retryCount} attempt(s). Giving up.");
                        break;
                    }
                    else
                    {
#if DEBUG
                        log.LogTrace($@"Posted message {messageToPost.Properties[@"MessageId"]} (Size: {messageToPost.Body.Count} bytes) in {retryCount} attempt(s)");
#else
                log.LogTrace($@"Posted message with TestRunID '{messageToPost.Properties[@"TestRunId"]}' and MessageId {messageToPost.Properties[@"MessageId"]} in {retryCount} attempt(s)");
#endif
                    }
                } while (retry);
            }

            return true;
        }
    }
}
