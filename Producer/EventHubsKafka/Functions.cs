using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Confluent.Kafka;
using Newtonsoft.Json;

namespace Producer.EventHubsKafka
{
    public static class Functions
    {
        [FunctionName(nameof(PostToEventHubKafka))]
        public static async Task<HttpResponseMessage> PostToEventHubKafka(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestMessage request,
            [OrchestrationClient]DurableOrchestrationClient client,
            ILogger log)
        {
            var inputObject = JObject.Parse(await request.Content.ReadAsStringAsync());
            var numberOfMessagesPerPartition = inputObject.Value<int>(@"NumberOfMessagesPerPartition");
            var numberOfPartitions = inputObject.Value<int>(@"NumberOfPartitions");

            var workTime = -1;
            if (inputObject.TryGetValue(@"WorkTime", out var workTimeVal))
            {
                workTime = workTimeVal.Value<int>();
            }

            var orchestrationIds = new List<string>();
            var testRunId = Guid.NewGuid().ToString();
            for (var c = 1; c <= numberOfPartitions; c++)
            {
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
            [OrchestrationTrigger]DurableOrchestrationContext ctx,
            ILogger log)
        {
            var req = ctx.GetInput<MessagesCreateRequest>();

            var messages = Enumerable.Range(1, req.NumberOfMessagesPerPartition)
                    .Select(m =>
                    {
                        var enqueueTime = DateTime.UtcNow;
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
                return JObject.FromObject(new { Error = $@"An error occurred executing orchestration {ctx.InstanceId}: {ex.ToString()}" });
            }
        }

        private const int MAX_RETRY_ATTEMPTS = 10;
        private static readonly Lazy<string> _messageContent = new Lazy<string>(() =>
        {
            using (var sr = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream($@"Producer.messagecontent.txt")))
            {
                return sr.ReadToEnd();
            }
        });

        [FunctionName(nameof(PostMessagesToEventHub))]
        public static async Task<bool> PostMessagesToEventHub([ActivityTrigger]DurableActivityContext ctx,
            ILogger log)
        {
            string brokerList = ConfigurationManager.AppSettings["EventHubKafkaFQDN"];
            string connectionString = ConfigurationManager.AppSettings["EventHubKafkaConnection"];
            string topic = ConfigurationManager.AppSettings["EventHubKafkaName"];

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
                        //new EventData(Encoding.Default.GetBytes(_messageContent.Value));
                        //r.Properties.Add(@"MessageId", m.MessageId);
                        //r.Properties.Add(@"EnqueueTimeUtc", m.EnqueueTimeUtc);
                        //r.Properties.Add(@"TestRunId", m.TestRunId);

                        //if (m.ConsumerWorkTime > 0)
                        //{
                        //    r.Properties.Add(@"workTime", m.ConsumerWorkTime);
                        //}

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
