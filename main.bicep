var dataExplorerClusterName = toLower('dex${uniqueString(subscription().id, resourceGroup().id)}')
var consumerFunctionAppName = 'consumer${uniqueString(subscription().id, resourceGroup().id)}'
var consumerFunctionAppNamev5 = 'consumer${uniqueString(subscription().id, resourceGroup().id)}5'
var consumerFunctionAppNamev6 = 'consumer${uniqueString(subscription().id, resourceGroup().id)}6'
var producerFunctionAppName = 'producer${uniqueString(subscription().id, resourceGroup().id)}'
var functionPlanName = '${uniqueString(subscription().id, resourceGroup().id)}Plan'
var storageAccountName = toLower('stor${uniqueString(subscription().id, resourceGroup().id)}')
var storageAccountName5 = toLower('stor5${uniqueString(subscription().id, resourceGroup().id)}')
var storageAccountName6 = toLower('stor6${uniqueString(subscription().id, resourceGroup().id)}')
var serviceBusNamespaceName = 'sb${uniqueString(subscription().id, resourceGroup().id)}'
var eventHubNamespaceName = 'eh${uniqueString(subscription().id, resourceGroup().id)}'
var eventHubKafkaNamespaceName = 'ehk${uniqueString(subscription().id, resourceGroup().id)}'
var eventGridTopicName = 'egt${uniqueString(subscription().id, resourceGroup().id)}'
var ehAuthRuleResourceId = resourceId('Microsoft.EventHub/namespaces/authorizationRules', eventHubNamespaceName, 'RootManageSharedAccessKey')
var ehkAuthRuleResourceId = resourceId('Microsoft.EventHub/namespaces/authorizationRules', eventHubKafkaNamespaceName, 'RootManageSharedAccessKey')
var sbAuthRuleResourceId = resourceId('Microsoft.ServiceBus/namespaces/authorizationRules', serviceBusNamespaceName, 'RootManageSharedAccessKey')
var sampleTags = {
  sample: 'azure-durable-functions-producer-consumer'
}

resource ehNamespace 'Microsoft.EventHub/namespaces@2017-04-01' = {
  name: eventHubNamespaceName
  location: resourceGroup().location
  tags: sampleTags
  sku: {
    name: 'Standard'
    tier: 'Standard'
    capacity: 1
  }
  properties: {
    isAutoInflateEnabled: true
    maximumThroughputUnits: 20
    kafkaEnabled: false
  }

  resource collectorEventHub 'eventhubs' = {
    name: 'collector'
    properties: {
      messageRetentionInDays: 1
      partitionCount: 32
      status: 'Active'
    }
  }

  resource sampleEventHub 'eventhubs' = {
    name: 'sample'
    properties: {
      messageRetentionInDays: 1
      partitionCount: 32
    }

    resource net3ConsumerGroup 'consumergroups' = {
      name: 'net3'
    }

    resource net5ConsumerGroup 'consumergroups' = {
      name: 'net5'
    }

    resource net6ConsumerGroup 'consumergroups' = {
      name: 'net6'
    }
  }
}

resource ehKafkaNamespace 'Microsoft.EventHub/namespaces@2017-04-01' = {
  name: eventHubKafkaNamespaceName
  location: resourceGroup().location
  tags: sampleTags
  sku: {
    name: 'Standard'
    tier: 'Standard'
    capacity: 13
  }
  properties: {
    isAutoInflateEnabled: true
    kafkaEnabled: true
    maximumThroughputUnits: 20
  }

  resource sampleEventHubKafka 'eventhubs' = {
    name: 'sample'
  }
}

resource kustoCluster 'Microsoft.Kusto/clusters@2021-01-01' = {
  name: dataExplorerClusterName
  location: resourceGroup().location
  sku: {
    name: 'Dev(No SLA)_Standard_D11_v2'
    tier: 'Basic'
    capacity: 1
  }
  tags: sampleTags

  resource kustoDatabase 'databases' = {
    name: 'sampledata'
    location: resourceGroup().location
    kind: 'ReadWrite'
    properties: {
      hotCachePeriod: 'P31D'
      softDeletePeriod: 'P3650D'
    }
  }
}

resource sbNamespace 'Microsoft.ServiceBus/namespaces@2017-04-01' = {
  name: serviceBusNamespaceName
  location: resourceGroup().location
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
  tags: sampleTags

  resource sbQueue 'queues' = {
    name: 'sample'
    properties: {
      enableBatchedOperations: true
      requiresSession: true
    }
  }
}

resource fxStorageAccount 'Microsoft.Storage/storageAccounts@2021-04-01' = {
  name: storageAccountName
  location: resourceGroup().location
  sku: {
    name: 'Standard_RAGRS'
  }
  kind: 'StorageV2'
  tags: sampleTags

  resource fxBlobServices 'blobServices' = {
    name: 'default'
  }
  resource fxQueueServices 'queueServices' = {
    name: 'default'
    resource sampleQueue 'queues' = {
      name: 'sample'
    }
  }
}

resource fxStorageAccount5 'Microsoft.Storage/storageAccounts@2021-04-01' = {
  name: storageAccountName5
  location: resourceGroup().location
  sku: {
    name: 'Standard_RAGRS'
  }
  kind: 'StorageV2'
  tags: sampleTags

  resource fxBlobServices 'blobServices' = {
    name: 'default'
  }
  resource fxQueueServices 'queueServices' = {
    name: 'default'
    resource sampleQueue 'queues' = {
      name: 'sample'
    }
  }
}

resource fxStorageAccount6 'Microsoft.Storage/storageAccounts@2021-04-01' = {
  name: storageAccountName6
  location: resourceGroup().location
  sku: {
    name: 'Standard_RAGRS'
  }
  kind: 'StorageV2'
  tags: sampleTags

  resource fxBlobServices 'blobServices' = {
    name: 'default'
  }
  resource fxQueueServices 'queueServices' = {
    name: 'default'
    resource sampleQueue 'queues' = {
      name: 'sample'
    }
  }
}

resource fxPlan 'Microsoft.Web/serverfarms@2021-01-15' = {
  name: functionPlanName
  location: resourceGroup().location
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  tags: sampleTags
}

resource appInsightsProducer 'Microsoft.Insights/components@2020-02-02' = {
  name: producerFunctionAppName
  location: resourceGroup().location
  kind: 'other'
  tags: sampleTags
  properties: {
    Application_Type: 'other'
  }
}

resource producerApp 'Microsoft.Web/sites@2021-01-15' = {
  name: producerFunctionAppName
  location: resourceGroup().location
  kind: 'functionapp'
  tags: sampleTags
  properties: {
    serverFarmId: fxPlan.id
    siteConfig: {
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: fxStorageConnectionString
        }
        {
          'name': 'APPINSIGHTS_INSTRUMENTATIONKEY'
          'value': appInsightsProducer.properties.InstrumentationKey
        }
        {
          'name': 'DFTaskHubName'
          'value': 'producerTaskHub'
        }
        {
          'name': 'EventHubConnection'
          'value': listkeys(ehAuthRuleResourceId, ehNamespace::sampleEventHub.apiVersion).primaryConnectionString
        }
        {
          'name': 'EventHubName'
          'value': ehNamespace::sampleEventHub.name
        }
        {
          'name': 'EventHubKafkaConnection'
          'value': listkeys(ehkAuthRuleResourceId, ehKafkaNamespace::sampleEventHubKafka.apiVersion).primaryConnectionString
        }
        {
          'name': 'EventHubKafkaName'
          'value': ehKafkaNamespace::sampleEventHubKafka.name
        }
        {
          'name': 'EventHubKafkaFQDN'
          'value': '${ehKafkaNamespace.name}.servicebus.windows.net:9093'
        }
        {
          'name': 'EventHubKafkaPartitions'
          'value': '32'
        }
        {
          'name': 'EventHubPartitions'
          'value': '32'
        }
        {
          'name': 'EventGridTopicEndpoint'
          'value': eventGridTopic.properties.endpoint
        }
        {
          'name': 'EventGridTopicKey'
          'value': listKeys(eventGridTopic.id, eventGridTopic.apiVersion).key1
        }
        {
          'name': 'FUNCTIONS_EXTENSION_RUNTIME'
          'value': 'dotnet'
        }
        {
          'name': 'FUNCTIONS_EXTENSION_VERSION'
          'value': '~3'
        }
        {
          'name': 'ServiceBusConnection'
          'value': listkeys(sbAuthRuleResourceId, sbNamespace::sbQueue.apiVersion).primaryConnectionString
        }
        {
          'name': 'ServiceBusQueueName'
          'value': sbNamespace::sbQueue.name
        }
        {
          'name': 'StorageQueueConnection'
          'value': fxStorageConnectionString
        }
        {
          'name': 'StorageQueueName'
          'value': fxStorageAccount::fxQueueServices::sampleQueue.name
        }
        {
          'name': 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          'value': fxStorageConnectionString
        }
        {
          'name': 'WEBSITE_CONTENTSHARE'
          'value': toLower(producerFunctionAppName)
        }
      ]
    }
  }
}

resource appInsightsConsumer 'Microsoft.Insights/components@2020-02-02' = {
  name: consumerFunctionAppName
  location: resourceGroup().location
  kind: 'other'
  tags: sampleTags
  properties: {
    Application_Type: 'other'
  }
}

var fxStorageConnectionString = 'DefaultEndpointsProtocol=https;AccountName=${fxStorageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${listKeys(fxStorageAccount.id, fxStorageAccount.apiVersion).keys[0].value}'

resource consumerApp 'Microsoft.Web/sites@2021-01-15' = {
  name: consumerFunctionAppName
  location: resourceGroup().location
  kind: 'functionapp'
  tags: sampleTags
  properties: {
    serverFarmId: fxPlan.id
    siteConfig: {
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: fxStorageConnectionString
        }
        {
          'name': 'APPINSIGHTS_INSTRUMENTATIONKEY'
          'value': appInsightsConsumer.properties.InstrumentationKey
        }
        {
          'name': 'CollectorEventHubConnection'
          'value': listkeys(ehAuthRuleResourceId, ehNamespace::collectorEventHub.apiVersion).primaryConnectionString
        }
        {
          'name': 'CollectorEventHubName'
          'value': ehNamespace::collectorEventHub.name
        }
        {
          'name': 'EventHubConnection'
          'value': listkeys(ehAuthRuleResourceId, ehNamespace::sampleEventHub.apiVersion).primaryConnectionString
        }
        {
          'name': 'EventHubName'
          'value': ehNamespace::sampleEventHub.name
        }
        {
          'name': 'EventHubConsumerGroupName'
          'value': ehNamespace::sampleEventHub::net3ConsumerGroup.name
        }
        {
          'name': 'FUNCTIONS_EXTENSION_RUNTIME'
          'value': 'dotnet'
        }
        {
          'name': 'FUNCTIONS_EXTENSION_VERSION'
          'value': '~3'
        }
        {
          'name': 'ServiceBusConnection'
          'value': listkeys(sbAuthRuleResourceId, sbNamespace::sbQueue.apiVersion).primaryConnectionString
        }
        {
          'name': 'ServiceBusQueueName'
          'value': sbNamespace::sbQueue.name
        }
        {
          'name': 'StorageQueueConnection'
          'value': fxStorageConnectionString
        }
        {
          'name': 'StorageQueueName'
          'value': fxStorageAccount::fxQueueServices::sampleQueue.name
        }
        {
          'name': 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          'value': fxStorageConnectionString
        }
        {
          'name': 'WEBSITE_CONTENTSHARE'
          'value': toLower(consumerFunctionAppName)
        }
      ]
    }
  }
}

resource appInsightsConsumerv5 'Microsoft.Insights/components@2020-02-02' = {
  name: consumerFunctionAppNamev5
  location: resourceGroup().location
  kind: 'other'
  tags: sampleTags
  properties: {
    Application_Type: 'other'
  }
}

var fxStorageConnectionString5 = 'DefaultEndpointsProtocol=https;AccountName=${fxStorageAccount5.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${listKeys(fxStorageAccount5.id, fxStorageAccount5.apiVersion).keys[0].value}'

resource consumerAppv5 'Microsoft.Web/sites@2021-01-15' = {
  name: consumerFunctionAppNamev5
  location: resourceGroup().location
  kind: 'functionapp'
  tags: sampleTags
  properties: {
    serverFarmId: fxPlan.id
    siteConfig: {
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: fxStorageConnectionString5
        }
        {
          'name': 'APPINSIGHTS_INSTRUMENTATIONKEY'
          'value': appInsightsConsumerv5.properties.InstrumentationKey
        }
        {
          'name': 'CollectorEventHubConnection'
          'value': listkeys(ehAuthRuleResourceId, ehNamespace::collectorEventHub.apiVersion).primaryConnectionString
        }
        {
          'name': 'CollectorEventHubName'
          'value': ehNamespace::collectorEventHub.name
        }
        {
          'name': 'EventHubConnection'
          'value': listkeys(ehAuthRuleResourceId, ehNamespace::sampleEventHub.apiVersion).primaryConnectionString
        }
        {
          'name': 'EventHubName'
          'value': ehNamespace::sampleEventHub.name
        }
        {
          'name': 'EventHubConsumerGroupName'
          'value': ehNamespace::sampleEventHub::net5ConsumerGroup.name
        }
        {
          'name': 'FUNCTIONS_EXTENSION_RUNTIME'
          'value': 'dotnet-isolated'
        }
        {
          'name': 'FUNCTIONS_EXTENSION_VERSION'
          'value': '~3'
        }
        {
          'name': 'ServiceBusConnection'
          'value': listkeys(sbAuthRuleResourceId, sbNamespace::sbQueue.apiVersion).primaryConnectionString
        }
        {
          'name': 'ServiceBusQueueName'
          'value': sbNamespace::sbQueue.name
        }
        {
          'name': 'StorageQueueConnection'
          'value': fxStorageConnectionString5
        }
        {
          'name': 'StorageQueueName'
          'value': fxStorageAccount5::fxQueueServices::sampleQueue.name
        }
        {
          'name': 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          'value': fxStorageConnectionString5
        }
        {
          'name': 'WEBSITE_CONTENTSHARE'
          'value': toLower(consumerFunctionAppNamev5)
        }
      ]
    }
  }
}

resource appInsightsConsumerv6 'Microsoft.Insights/components@2020-02-02' = {
  name: consumerFunctionAppNamev6
  location: resourceGroup().location
  kind: 'other'
  tags: sampleTags
  properties: {
    Application_Type: 'other'
  }
}

var fxStorageConnectionString6 = 'DefaultEndpointsProtocol=https;AccountName=${fxStorageAccount6.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${listKeys(fxStorageAccount6.id, fxStorageAccount6.apiVersion).keys[0].value}'

resource consumerAppv6 'Microsoft.Web/sites@2021-01-15' = {
  name: consumerFunctionAppNamev6
  location: resourceGroup().location
  kind: 'functionapp'
  tags: sampleTags
  properties: {
    serverFarmId: fxPlan.id
    siteConfig: {
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: fxStorageConnectionString6
        }
        {
          'name': 'APPINSIGHTS_INSTRUMENTATIONKEY'
          'value': appInsightsConsumerv6.properties.InstrumentationKey
        }
        {
          'name': 'CollectorEventHubConnection'
          'value': listkeys(ehAuthRuleResourceId, ehNamespace::collectorEventHub.apiVersion).primaryConnectionString
        }
        {
          'name': 'CollectorEventHubName'
          'value': ehNamespace::collectorEventHub.name
        }
        {
          'name': 'EventHubConnection'
          'value': listkeys(ehAuthRuleResourceId, ehNamespace::sampleEventHub.apiVersion).primaryConnectionString
        }
        {
          'name': 'EventHubName'
          'value': ehNamespace::sampleEventHub.name
        }
        {
          'name': 'EventHubConsumerGroupName'
          'value': ehNamespace::sampleEventHub::net6ConsumerGroup.name
        }
        {
          'name': 'FUNCTIONS_EXTENSION_RUNTIME'
          'value': 'dotnet-isolated'
        }
        {
          'name': 'FUNCTIONS_EXTENSION_VERSION'
          'value': '~4'
        }
        {
          'name': 'ServiceBusConnection'
          'value': listkeys(sbAuthRuleResourceId, sbNamespace::sbQueue.apiVersion).primaryConnectionString
        }
        {
          'name': 'ServiceBusQueueName'
          'value': sbNamespace::sbQueue.name
        }
        {
          'name': 'StorageQueueConnection'
          'value': fxStorageConnectionString6
        }
        {
          'name': 'StorageQueueName'
          'value': fxStorageAccount6::fxQueueServices::sampleQueue.name
        }
        {
          'name': 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          'value': fxStorageConnectionString6
        }
        {
          'name': 'WEBSITE_CONTENTSHARE'
          'value': toLower(consumerFunctionAppNamev6)
        }
      ]
    }
  }
}

resource eventGridTopic 'Microsoft.EventGrid/topics@2020-10-15-preview' = {
  name: eventGridTopicName
  location: resourceGroup().location
  tags: sampleTags
  sku: {
    name: 'Basic'
  }
  kind: 'Azure'
  properties: {
    inputSchema: 'EventGridSchema'
    publicNetworkAccess: 'Enabled'
  }
}

output consumerApp string = consumerApp.name
output consumerAppv5 string = consumerAppv5.name
output consumerAppv6 string = consumerAppv6.name
output producerApp string = producerApp.name
output dexbaseUrl string = 'https://dataexplorer.azure.com/clusters/${kustoCluster.name}.${resourceGroup().location}/databases/${kustoCluster::kustoDatabase.name}'
output dexResourceHost string = '${kustoCluster.name}.${resourceGroup().location}.kusto.windows.net'
output eventHubProducer string = 'https://${producerApp.name}.azurewebsites.net/api/PostToEventHub?code=${listkeys('${producerApp.id}/host/default/', producerApp.apiVersion).functionKeys.default}'
output eventHubKafkaProducer string = 'https://${producerApp.name}.azurewebsites.net/api/PostToEventHubKafka?code=${listkeys('${producerApp.id}/host/default/', producerApp.apiVersion).functionKeys.default}'
output serviceBusProducer string = 'https://${producerApp.name}.azurewebsites.net/api/PostToServiceBusQueue?code=${listkeys('${producerApp.id}/host/default/', producerApp.apiVersion).functionKeys.default}'
output storageQueueProducer string = 'https://${producerApp.name}.azurewebsites.net/api/PostToStorageQueue?code=${listkeys('${producerApp.id}/host/default/', producerApp.apiVersion).functionKeys.default}'
output eventGridProducer string = 'https://${producerApp.name}.azurewebsites.net/api/PostToEventGrid?code=${listkeys('${producerApp.id}/host/default/', producerApp.apiVersion).functionKeys.default}'
output storageAccountName string = fxStorageAccount.name
