---
page_type: sample
languages:
- csharp
products:
- azure
- dotnet
- azure-functions
- azure-event-hubs
- azure-service-bus
- azure-storage
name: "Produce and Consume messages through Service Bus, Event Hubs, and Storage Queues with Azure Functions"
description: "Uses Durable Functions' fan out pattern to load N messages across M sessions in to Service Bus, Event Hubs, or Storage Queues."
urlFragment: product-consume-messages-az-functions
---

# Produce & Consume messages through Service Bus, Event Hubs, and Storage Queues with Durable Functions

This sample shows how to utilize Durable Functions' fan out pattern to load an arbitrary number of messages across any number of sessions/partitions in to Service Bus, Event Hubs, or Storage Queues. It also adds the ability to consume those messages with another Azure Function and load the resulting timing data in to another Event Hub for ingestion in to analytics services like Azure Data Explorer.

> **Note**: Please use the `sample.local.settings.json` file as the baseline for `local.settings.json` when testing this sample locally.

## Service Bus

```http
POST /api/PostToServiceBusQueue HTTP/1.1
Content-Type: application/json
cache-control: no-cache

{
  "NumberOfSessions": 2,
  "NumberOfMessagesPerSession": 2
}
```

Will post two messages across two sessions to the Service Bus queue specified by the `ServiceBusConnection` and `ServiceBusQueueName` settings in your `local.settings.json` file or - when published to Azure - the Function App's application settings.

## Event Hubs

```http
POST /api/PostToEventHub HTTP/1.1
Content-Type: application/json
cache-control: no-cache

{
  "NumberOfMessagesPerPartition": 2
}
```

Will post two messages across per partition to the Event Hub specified by the `EventHubConnection` and `EventHubName` settings in your `local.settings.json` file or - when published to Azure - the Function App's application settings.

The number of messages per partition will differ by no more than 1. Which means that at any given point of time, if the number of messages expected to be sent per partition is say `x`, then you can expect that there may be some partitions with number of messages `x+1` and some with number of messages `x-1`.

## Event Hubs Kafka

```http
POST /api/PostToEventHubKafka HTTP/1.1
Content-Type: application/json
cache-control: no-cache

{
  "NumberOfMessagesPerPartition": 2
}
```

Will post two messages per partition of the Event Hub specified by the `EventHubKafkaConnection`, `EventHubKafkaName` and `EventHubKafkaFQDN` settings in your `local.settings.json` file or - when published to Azure - the Function App's application settings.
The number of messages per partition will differ by no more than 1. Which means that at any given point of time, if the number of messages expected to be sent per partition is say `x`, then you can expect that there may be some partitions with number of messages `x+1` and some with number of messages `x-1`.
The Event Hubs Kafka sample just has producer at this point. You can observe the messages coming in using Azure Monitor from the portal.

## Storage Queues

```http
POST /api/PostToStorageQueue HTTP/1.1
Content-Type: application/json
cache-control: no-cache

{
  "NumberOfMessages": 2
}
```

Will post two messages to the Storage Queue specified by the `StorageQueueConnection` and `StorageQueueName` settings in your `local.settings.json` file or - when published to Azure - the Function App's application settings.

## Event Grid

```http
POST /api/PostToEventGrid HTTP/1.1
Content-Type: application/json
cache-control: no-cache

{
    "NumberOfMessages": 2
}
```

Will post two messages to the Event Grid Topic specified by the `EventGridTopicEndpoint` and `EventGridTopicKey` settings in your `local.settings.json` file or - when published to Azure - the Function App's application settings.

## Implementation

### Fan out/in

This sample utilizes [Durable Functions fan out/in pattern](https://docs.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-cloud-backup) to produce messages in parallel across the sessions/partitions you specify. For this reason, pay close attention to [the `DFTaskHubName` application setting](Producer/sample.local.settings.json) if you put it in the same Function App as other Durable Function implementation.

### Message content

The content for each message is **not** dynamic at this time. It is simply stored in the [messagecontent.txt](Producer/messagecontent.txt) file and posted as the content for each message in every scenario. If you wish to make the content dynamic, you can do so by changing the code in each scenario's `Functions.cs` file of the `Producer` project.

## Demo

This project comes out of a customer engagement whereby we wanted to see how long it would take messages loaded in to a Service Bus queue across thousands of sessions to be processed in FIFO order per session.

### Deploy to Azure

1. Open the solution in GitHub Codespaces or VS Code Dev Containers
1. Login to Azure by running `az login`
1. Execute `.\deploy.ps1 <subscription id> <resource group name> [location]`

Upon completion, your subscription will have the Resource Group you named above with:

* Service Bus **Standard** namespace with a `sample` queue
* Event Hub **Basic** namespace
  * A `collector` hub w/ 32 partitions - this is where each consumer posts messages when they consume from their source
  * A `sample` hub with 32 partitions - this is where the Producer will post messages for the EH scenario
    * A `net5` consumer group for the .NET5-based Consumer Function
    * A `net3` consumer group for the .NET3-based Consumer Function
* Event Hub **Standard** namespace with Kafka enabled
  * A `sample` hub with 32 partitions - this is where the Producer will post messages using Kafka protocol for the EH-Kafka scenario
* Azure Data Explorer **Dev** instance ingesting data from the above Event Hub
* Azure Storage instance for use by the Durable Functions and the Storage Queue producer/consumer paths (`sample` queue created)
* 1 Azure Function app with the Producer Function code
  * Application settings set to the connection strings of the Service Bus, Event Hub, and Azure Storage
* 1 Azure Function app with the .NET 3 (in-proc worker) Consumer Function code
  * Application settings set to the connection strings of the Service Bus, Event Hub, and Azure Storage
* 1 Azure Function app with the .NET 5 (out-of-proc worker) Consumer Function code
  * Application settings set to the connection strings of the Service Bus, Event Hub, and Azure Storage

> Note: The deployment script sets up an initial deploy from GitHub to the created Function Apps. If you wish to change any behavior of this sample, you will need to manually publish your changes _after_ first deploying the sample and then disconnecting the `ExternalGit` deployment from the Function App you're modifying. Additionally, any subsequent executions of `deploy.ps1` will reset the code to the state of `main` in this repo.

Upon successful deployment you'll be given the HTTP POST URLs for each of the Producer endpoints. Using the sample payloads earlier in this Readme, you'll get a response like:

```json
{
    "TestRunId": "b5f4b4ef-75db-4586-adc0-b66da97a6545"
}
```

Then, head to your Data Explorer (the window you ran queries within during deployment) and execute the following query to see the results of your run:

```kusto
SampleDataTable
```

> Note: It can take up to 5 minutes for data to be ingested & show up in Azure Data Explorer

to retrieve all of the rows in the ingestion table. You should then see something like this:
![Sample output from above KQL query](doc-img/all-sampledata.png)

You can use the content of `Properties` to get more detailed data. Try this query:

```kusto
SampleDataTable
| extend Duration = make_timespan(MessageProcessedTime - Properties.ClientEnqueueTimeUtc)
| summarize AvgIndividualProcessTime = avg(Duration), RunStartTime = min(make_datetime(Properties.ClientEnqueueTimeUtc)), RunEndTime = max(MessageProcessedTime) by TestRun, Trigger
| extend TotalRuntime = (RunEndTime - RunStartTime)
| project-away RunStartTime, RunEndTime
```

This will show you the average processing time for an individual message in the test run, and then the overall time for the entire test run to complete. You can limit to a specific test run by adding a `where` filter:

```kusto
SampleDataTable
| where TestRun = 'b5f4b4ef-75db-4586-adc0-b66da97a6545'
| extend Duration = make_timespan(MessageProcessedTime - Properties.ClientEnqueueTimeUtc)
| summarize AvgIndividualProcessTime = avg(Duration), RunStartTime = min(make_datetime(Properties.ClientEnqueueTimeUtc)), RunEndTime = max(MessageProcessedTime) by TestRun, Trigger
| extend TotalRuntime = (RunEndTime - RunStartTime)
| project-away RunStartTime, RunEndTime
```
