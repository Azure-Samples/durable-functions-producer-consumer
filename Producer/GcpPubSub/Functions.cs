using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Producer.GcpPubSub
{
    public static class Functions
    {
        private static PublisherClient _pubSubClient;

        [FunctionName(nameof(PostToPubSub))]
        public static async Task<HttpResponseMessage> PostToPubSub(
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

            if (_pubSubClient == null)
            {
                _pubSubClient = await PublisherClient.CreateAsync(new TopicName(Environment.GetEnvironmentVariable(@"GcpProjectId"), Environment.GetEnvironmentVariable(@"GcpTopicId")));
            }

            var testRunId = Guid.NewGuid().ToString();
            var orchId = await client.StartNewAsync(nameof(GenerateMessagesForPubSub),
                    (numberOfMessages, testRunId, workTime));

            log.LogTrace($@"Kicked off {numberOfMessages} message creation...");

            return await client.WaitForCompletionOrCreateCheckStatusResponseAsync(request, orchId, TimeSpan.FromMinutes(2));
        }

        [FunctionName(nameof(GenerateMessagesForPubSub))]
        public static async Task<JObject> GenerateMessagesForPubSub(
            [OrchestrationTrigger]DurableOrchestrationContext ctx,
            ILogger log)
        {
            var req = ctx.GetInput<(int numOfMessages, string testRunId, int workTime)>();

            var activities = Enumerable.Empty<Task<bool>>().ToList();
            for (var i = 0; i < req.numOfMessages; i++)
            {
                try
                {
                    activities.Add(ctx.CallActivityAsync<bool>(nameof(PostMessageToPubSub), (i, req.testRunId, req.workTime)));
                }
                catch (Exception ex)
                {
                    log.LogError(ex, @"An error occurred queuing message generation to PubSub");
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

        [FunctionName(nameof(PostMessageToPubSub))]
        public static async Task<bool> PostMessageToPubSub([ActivityTrigger]DurableActivityContext ctx,
            ILogger log)
        {
            var msgDetails = ctx.GetInput<(int id, string runId, int workTime)>();
            var retryCount = 0;
            var retry = false;

            var messageToPost = new PubsubMessage
            {
                Data = ByteString.CopyFromUtf8(_messageContent.Value),
                MessageId = msgDetails.id.ToString(),
            };
            messageToPost.Attributes.Add(@"EnqueueTimeUtc", DateTime.UtcNow.ToLongTimeString());
            messageToPost.Attributes.Add(@"TestRunId", msgDetails.runId);

            if (msgDetails.workTime > 0)
            {
                messageToPost.Attributes.Add(@"workTime", msgDetails.workTime.ToString());
            }

            do
            {
                retryCount++;
                try
                {
                    await _pubSubClient.PublishAsync(messageToPost);
                    retry = false;
                }
                catch (Exception ex)
                {
                    log.LogError(ex, $@"Error posting message {messageToPost.MessageId}. Retrying...");
                    retry = true;
                }

                if (retry && retryCount >= MAX_RETRY_ATTEMPTS)
                {
                    log.LogError($@"Unable to post message {messageToPost.MessageId} after {retryCount} attempt(s). Giving up.");
                    break;
                }
                else
                {
#if DEBUG
                    log.LogTrace($@"Posted message {messageToPost.MessageId} (Size: {_messageContent.Value.Length} bytes) in {retryCount} attempt(s)");
#else
                log.LogTrace($@"Posted message in {retryCount} attempt(s)");
#endif
                }
            } while (retry);

            return true;
        }
    }
}
