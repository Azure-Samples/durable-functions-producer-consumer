using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Producer.AmazonSqs
{
    public static class Functions
    {
        [FunctionName(nameof(PostToSimpleQueue))]
        public static async Task<HttpResponseMessage> PostToSimpleQueue(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestMessage request,
            [OrchestrationClient]DurableOrchestrationClient client,
            ILogger log)
        {
            var inputObject = JObject.Parse(await request.Content.ReadAsStringAsync());
            var numberOfMessagesPerGroup = inputObject.Value<int>(@"NumberOfMessagesPerGroup");
            var numberOfGroups = inputObject.Value<int>(@"NumberOfGroups");

            var workTime = -1;
            if (inputObject.TryGetValue(@"WorkTime", out var workTimeVal))
            {
                workTime = workTimeVal.Value<int>();
            }

            var orchestrationIds = new List<string>();
            var testRunId = Guid.NewGuid().ToString();
            for (var c = 1; c <= numberOfGroups; c++)
            {
                var groupId = Guid.NewGuid().ToString();
                var orchId = await client.StartNewAsync(nameof(GenerateMessagesForSqsGroup),
                    new GroupCreateRequest
                    {
                        TestRunId = testRunId,
                        GroupId = groupId,
                        NumberOfMessagesPerGroup = numberOfMessagesPerGroup,
                        ConsumerWorkTime = workTime,
                    });

                log.LogTrace($@"Kicked off message creation for group {groupId}...");

                orchestrationIds.Add(orchId);
            }

            return await client.WaitForCompletionOrCreateCheckStatusResponseAsync(request, orchestrationIds.First(), TimeSpan.FromMinutes(2));
        }

        [FunctionName(nameof(GenerateMessagesForSqsGroup))]
        public static async Task<JObject> GenerateMessagesForSqsGroup(
            [OrchestrationTrigger]DurableOrchestrationContext ctx,
            ILogger log)
        {
            var req = ctx.GetInput<GroupCreateRequest>();

            var messages = Enumerable.Range(1, req.NumberOfMessagesPerGroup)
                    .Select(m =>
                    {
                        return new GroupMessagesCreateRequest
                        {
                            GroupId = req.GroupId,
                            MessageId = m,
                            EnqueueTimeUtc = DateTime.UtcNow,
                            TestRunId = req.TestRunId,
                            ConsumerWorkTime = req.ConsumerWorkTime,
                        };
                    }).ToList();

            try
            {
                return await ctx.CallActivityAsync<bool>(nameof(PostMessagesToSimpleQueue), messages)
                    ? JObject.FromObject(new { req.TestRunId })
                    : JObject.FromObject(new { Error = $@"An error occurred executing orchestration {ctx.InstanceId}" });
            }
            catch (Exception ex)
            {
                log.LogError(ex, @"An error occurred queuing message generation to Simple Queue");
                return JObject.FromObject(new { Error = $@"An error occurred executing orchestration {ctx.InstanceId}: {ex.ToString()}" });
            }
        }

        private static readonly Lazy<string> _messageContent = new Lazy<string>(() =>
        {
            using (var sr = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream($@"Producer.messagecontent.txt")))
            {
                return sr.ReadToEnd();
            }
        });

        private const int MAX_RETRY_ATTEMPTS = 10;

        private static readonly Lazy<AmazonSQSClient> _sqsClient = new Lazy<AmazonSQSClient>(() => new AmazonSQSClient(new AmazonSQSConfig
        {
            ServiceURL = Environment.GetEnvironmentVariable(@"SqsServiceUrl"),
        }));

        private static string _sqsQueueUrl;
        private static async Task<string> GetQueueUrl()
        {
            if (string.IsNullOrWhiteSpace(_sqsQueueUrl))
            {
                var request = new GetQueueUrlRequest
                {
                    QueueName = Environment.GetEnvironmentVariable(@"SqsQueueName"),
                    QueueOwnerAWSAccountId = Environment.GetEnvironmentVariable(@"AwsAccountId")
                };

                var response = await _sqsClient.Value.GetQueueUrlAsync(request);
                _sqsQueueUrl = response.QueueUrl;
            }

            return _sqsQueueUrl;
        }

        [FunctionName(nameof(PostMessagesToSimpleQueue))]
        public static async Task<bool> PostMessagesToSimpleQueue([ActivityTrigger]DurableActivityContext ctx,
            ILogger log)
        {
            var messages = ctx.GetInput<IEnumerable<GroupMessagesCreateRequest>>();

            foreach (var messageToPost in messages.Select(async m =>
            {
                var r = new SendMessageRequest
                {
                    MessageBody = _messageContent.Value,
                    QueueUrl = await GetQueueUrl(),
                    MessageAttributes = new Dictionary<string, MessageAttributeValue>
                    {
                        { @"TestRunId", new MessageAttributeValue { DataType = @"String", StringValue = m.TestRunId } },
                        { @"GroupId", new MessageAttributeValue { DataType = @"String", StringValue = m.GroupId } },
                        { @"MessageId", new MessageAttributeValue { DataType = @"Number", StringValue = m.MessageId.ToString() } },
                        { @"ScheduledEnqueueTimeUtc", new MessageAttributeValue { DataType = @"String", StringValue = m.EnqueueTimeUtc.ToLongTimeString() } },
                    }
                };

                if (m.ConsumerWorkTime > 0)
                {
                    r.MessageAttributes.Add(@"workTime", new MessageAttributeValue { DataType = @"Number", StringValue = m.ConsumerWorkTime.ToString() });
                }

                return r;
            }))
            {
                var retryCount = 0;
                var retry = false;
                var msg = await messageToPost;

                do
                {
                    retryCount++;
                    try
                    {
                        await _sqsClient.Value.SendMessageAsync(msg);
                        retry = false;
                    }
                    catch (Exception ex)
                    {
                        log.LogError(ex, $@"Error posting message {msg.MessageAttributes[@"MessageId"].StringValue}. Retrying...");
                        retry = true;
                    }

                    if (retry && retryCount >= MAX_RETRY_ATTEMPTS)
                    {
                        log.LogError($@"Unable to post message {msg.MessageAttributes[@"MessageId"].StringValue} after {retryCount} attempt(s). Giving up.");
                        break;
                    }
                    else
                    {
#if DEBUG
                        log.LogTrace($@"Posted message {msg.MessageAttributes[@"MessageId"].StringValue} (Size: {_messageContent.Value.Length} bytes) in {retryCount} attempt(s)");
#else
                log.LogTrace($@"Posted message in {retryCount} attempt(s)");
#endif
                    }
                } while (retry);
            }

            return true;
        }
    }
}
