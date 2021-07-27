var dataExplorerClusterName = toLower('dex${uniqueString(subscription().id, resourceGroup().id)}')
var eventHubNamespaceName = 'eh${uniqueString(subscription().id, resourceGroup().id)}'
var consumerFunctionAppName = 'consumer${uniqueString(subscription().id, resourceGroup().id)}'
var eventGridTopicName = 'egt${uniqueString(subscription().id, resourceGroup().id)}'

resource sampledataCollector 'Microsoft.Kusto/clusters/databases/dataconnections@2019-11-09' = {
  name: '${dataExplorerClusterName}/sampledata/collector'
  location: resourceGroup().location
  kind: 'EventHub'
  properties: {
    eventHubResourceId: resourceId('Microsoft.EventHub/namespaces/eventhubs', eventHubNamespaceName, 'collector')
    consumerGroup: '$Default'
    tableName: 'SampleDataTable'
    mappingRuleName: 'DataMapping'
    dataFormat: 'JSON'
  }
}

resource egTopic 'Microsoft.EventGrid/topics@2020-06-01' existing = {
  name: eventGridTopicName
}
resource consumerAppSubscription 'Microsoft.EventGrid/eventSubscriptions@2020-06-01' = {
  scope: egTopic
  name: consumerFunctionAppName
  properties: {
    destination: {
      endpointType: 'AzureFunction'
      properties: {
        resourceId: resourceId('Microsoft.Web/Sites/functions', consumerFunctionAppName, 'EventGridProcessorAsync')
      }
    }
    eventDeliverySchema: 'EventGridSchema'
  }
}

resource consumerAppSubscription5 'Microsoft.EventGrid/eventSubscriptions@2020-06-01' = {
  scope: egTopic
  name: '${consumerFunctionAppName}5'
  properties: {
    destination: {
      endpointType: 'AzureFunction'
      properties: {
        resourceId: resourceId('Microsoft.Web/Sites/functions', '${consumerFunctionAppName}5', 'EventGridProcessorAsync')
      }
    }
    eventDeliverySchema: 'EventGridSchema'
  }
}
