using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Confluent.Kafka;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Producer.EventHubsKafka
{
    public static class Functions
    {
        [FunctionName(nameof(PostToEventHubKafka))]
        public static async Task<IActionResult> PostToEventHubKafka(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest request,
            [DurableClient] IDurableOrchestrationClient client,
            ILogger log)
        {
            var inputObject = JObject.Parse(await request.ReadAsStringAsync());
            var numberOfMessagesPerPartition = inputObject.Value<int>(@"NumberOfMessagesPerPartition");
            var numberOfPartitions = Convert.ToInt32(Environment.GetEnvironmentVariable("EventHubKafkaPartitions"));

            var workTime = -1;
            if (inputObject.TryGetValue(@"WorkTime", out var workTimeVal))
            {
                workTime = workTimeVal.Value<int>();
            }

            var orchestrationIds = new List<string>();
            var testRunId = Guid.NewGuid().ToString();
            for (var c = 1; c <= numberOfPartitions; c++)
            {
                var orchId = await client.StartNewAsync(nameof(GenerateMessagesForEventHubKafka),
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

        [FunctionName(nameof(GenerateMessagesForEventHubKafka))]
        public static async Task<JObject> GenerateMessagesForEventHubKafka(
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
                return await ctx.CallActivityAsync<bool>(nameof(PostMessagesToEventHubKafka), messages)
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

        [FunctionName(nameof(PostMessagesToEventHubKafka))]
        public static async Task<bool> PostMessagesToEventHubKafka([ActivityTrigger] IDurableActivityContext ctx,
            ILogger log)
        {
            string brokerList = Environment.GetEnvironmentVariable("EventHubKafkaFQDN");
            string connectionString = Environment.GetEnvironmentVariable("EventHubKafkaConnection");
            string topic = Environment.GetEnvironmentVariable("EventHubKafkaName");

            var config = new ProducerConfig
            {
                BootstrapServers = brokerList,
                SecurityProtocol = SecurityProtocol.SaslSsl,
                SaslMechanism = SaslMechanism.Plain,
                SaslUsername = "$ConnectionString",
                SaslPassword = connectionString
            };
#if DEBUG
            config.Debug = "security,broker,protocol";
#endif

            var messages = ctx.GetInput<IEnumerable<MessagesSendRequest>>();

            using (var producer = new ProducerBuilder<long, string>(config).SetKeySerializer(Serializers.Int64).SetValueSerializer(Serializers.Utf8).Build())
            {
                foreach (var messageToPost in messages.Select(m =>
                    {
                        var r = new MessagesSendRequest();
                        if (m.ConsumerWorkTime > 0)
                        {
                            r.ConsumerWorkTime = m.ConsumerWorkTime;
                        }
                        r.EnqueueTimeUtc = m.EnqueueTimeUtc;
                        r.MessageId = m.MessageId;
                        r.TestRunId = m.TestRunId;
                        r.Message = _messageContent.Value;

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
                            var msg = JsonConvert.SerializeObject(messageToPost);
                            var deliveryReport = await producer.ProduceAsync(topic, new Message<long, string> { Key = DateTime.UtcNow.Ticks, Value = msg });
                            retry = false;
                        }
                        catch (Exception ex)
                        {
                            log.LogError(ex, $@"Error posting message with TestRunID '{messageToPost.TestRunId}' and MessageID '{messageToPost.MessageId}'. Retrying...");
                            retry = true;
                        }

                        if (retry && retryCount >= MAX_RETRY_ATTEMPTS)
                        {
                            log.LogError($@"Unable to post message with TestRunID '{messageToPost.TestRunId}' and MessageID '{messageToPost.MessageId}' after {retryCount} attempt(s). Giving up.");
                            break;
                        }
                        else
                        {
#if DEBUG
                            log.LogTrace($@"Posted message {messageToPost.MessageId} (Size: {messageToPost.Message.Length} bytes) in {retryCount} attempt(s)");
#else
                log.LogTrace($@"Posted message for with TestRunID '{messageToPost.Properties[@"TestRunId"]}' and MessageID '{messageToPost.Properties[@"MessageId"]}' in {retryCount} attempt(s)");
#endif
                        }
                    } while (retry);
                }
            }

            return true;
        }
    }
}
