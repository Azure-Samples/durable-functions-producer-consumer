using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Producer.StorageQueues
{
    public static class Functions
    {
        [FunctionName(nameof(PostToStorageQueue))]
        public static async Task<HttpResponseMessage> PostToStorageQueue(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestMessage request,
            [OrchestrationClient]DurableOrchestrationClient client,
            ILogger log)
        {
            var inputObject = JObject.Parse(await request.Content.ReadAsStringAsync());
            var numberOfMessages = inputObject.Value<int>(@"NumberOfMessages");

            var workTime = -1;
            if (inputObject.TryGetValue(@"WorkTime", out var workTimeVal))
            {
                workTime = workTimeVal.Value<int>();
            }

            var testRunId = Guid.NewGuid().ToString();
            var orchId = await client.StartNewAsync(nameof(GenerateMessagesForStorageQueue),
                    (numberOfMessages, testRunId, workTime));

            log.LogTrace($@"Kicked off {numberOfMessages} message creation...");

            return await client.WaitForCompletionOrCreateCheckStatusResponseAsync(request, orchId, TimeSpan.FromMinutes(2));
        }

        [FunctionName(nameof(GenerateMessagesForStorageQueue))]
        public static async Task<JObject> GenerateMessagesForStorageQueue(
            [OrchestrationTrigger]DurableOrchestrationContext ctx,
            ILogger log)
        {
            var req = ctx.GetInput<(int numOfMessages, string testRunId, int workTime)>();

            var activities = Enumerable.Empty<Task<bool>>().ToList();
            for (var i = 0; i < req.numOfMessages; i++)
            {
                try
                {
                    activities.Add(ctx.CallActivityAsync<bool>(nameof(PostMessageToStorageQueue), (i, req.testRunId, req.workTime)));
                }
                catch (Exception ex)
                {
                    log.LogError(ex, @"An error occurred queuing message generation to Storage Queue");
                    return JObject.FromObject(new { Error = $@"An error occurred executing orchestration {ctx.InstanceId}: {ex.ToString()}" });
                }
            }

            return (await Task.WhenAll(activities)).All(r => r)    // return 'true' if all are 'true', 'false' otherwise
                    ? JObject.FromObject(new { TestRunId = req.testRunId })
                    : JObject.FromObject(new { Error = $@"An error occurred executing orchestration {ctx.InstanceId}" });
        }

        private const int MAX_RETRY_ATTEMPTS = 10;
        private static readonly Lazy<string> _messageContent = new Lazy<string>(() =>
        {
            using (var sr = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream($@"Producer.messagecontent.txt")))
            {
                return sr.ReadToEnd();
            }
        });

        [FunctionName(nameof(PostMessageToStorageQueue))]
        public static async Task<bool> PostMessageToStorageQueue([ActivityTrigger]DurableActivityContext ctx,
            [Queue("%StorageQueueName%", Connection = @"StorageQueueConnection")]IAsyncCollector<JObject> queueMessages,
            ILogger log)
        {
            var msgDetails = ctx.GetInput<(int id, string runId, int workTime)>();
            var retryCount = 0;
            var retry = false;

            var messageToPost = JObject.FromObject(new
            {
                Content = _messageContent.Value,
                EnqueueTimeUtc = DateTime.UtcNow,
                MessageId = msgDetails.id,
                TestRunId = msgDetails.runId
            });

            if (msgDetails.workTime > 0)
            {
                messageToPost.Add(@"workTime", msgDetails.workTime);
            }

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
                    log.LogError(ex, $@"Error posting message {messageToPost.Value<int>(@"MessageId")}. Retrying...");
                    retry = true;
                }

                if (retry && retryCount >= MAX_RETRY_ATTEMPTS)
                {
                    log.LogError($@"Unable to post message {messageToPost.Value<int>(@"MessageId")} after {retryCount} attempt(s). Giving up.");
                    break;
                }
                else
                {
#if DEBUG
                    log.LogTrace($@"Posted message {messageToPost.Value<int>(@"MessageId")} (Size: {_messageContent.Value.Length} bytes) in {retryCount} attempt(s)");
#else
                log.LogTrace($@"Posted message in {retryCount} attempt(s)");
#endif
                }
            } while (retry);

            return true;
        }
    }
}
