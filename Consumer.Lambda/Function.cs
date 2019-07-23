using System;
using System.Text;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Microsoft.Azure.EventHubs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace Consumer.Lambda
{
    public class Function
    {
        private const string CollectorEventHubConnection = "Endpoint=sb://cloudperftest-brandon.servicebus.windows.net/;SharedAccessKeyName=code;SharedAccessKey=3dBInyLYjlHAJgt9Kj+ox85Ar2o0oVbe7A0ImURw+f4=;EntityPath=outputv2";

        private static readonly EventHubClient _ehClient = EventHubClient.CreateFromConnectionString(CollectorEventHubConnection);

        /// <summary>
        /// Default constructor. This constructor is used by Lambda to construct the instance. When invoked in a Lambda environment
        /// the AWS credentials will come from the IAM role associated with the function and the AWS region will be set to the
        /// region the Lambda function is executed in.
        /// </summary>
        public Function()
        {

        }


        /// <summary>
        /// This method is called for every Lambda invocation. This method takes in an SQS event object and can be used 
        /// to respond to SQS messages.
        /// </summary>
        /// <param name="evnt"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task FunctionHandler(SQSEvent evnt, ILambdaContext context)
        {
            foreach (var message in evnt.Records)
            {
                await ProcessMessageAsync(message, context);
            }
        }

        private static readonly string _instanceId = Guid.NewGuid().ToString();

        private async Task ProcessMessageAsync(SQSEvent.SQSMessage message, ILambdaContext context)
        {
            context.Logger.LogLine($"Processing message {message.Body} ...");

            var jsonBody = JObject.Parse(message.Body);
            jsonBody.Add(@"ExecutionId", Guid.NewGuid().ToString());
            jsonBody.Add(@"InstanceId", _instanceId);
            jsonBody.Add(@"Language", @"csharp");
            jsonBody.Add(@"TestRunId", jsonBody.Value<string>(@"TestRunId"));

            await Task.Delay(jsonBody.Value<int>(@"ConsumerWorkTimeMs"));

            var eventHubMessage = new AwsCollectorMessage
            {
                MessageProcessedTime = DateTime.UtcNow,
                TestRun = jsonBody.Value<string>(@"TestRunId"),
                MessageNumber = jsonBody.Value<int>(@"MessageNumber"),
                PublishTime = jsonBody.Value<DateTime>(@"PublishTime"),
                Trigger = @"Queue",
                Properties = jsonBody,
            };

            await _ehClient.SendAsync(new EventData(Encoding.Default.GetBytes(JsonConvert.SerializeObject(eventHubMessage))));

            context.Logger.LogLine($@"Processed to EventHub as:
{JsonConvert.SerializeObject(eventHubMessage, Formatting.Indented)}");

            context.Logger.LogLine($"Processed message {eventHubMessage.MessageNumber} for run {eventHubMessage.TestRun}");
        }
    }
}
