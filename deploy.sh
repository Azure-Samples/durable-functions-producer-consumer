#! /bin/bash

subscriptionId=$1
resourceGroupName=$2
location=${3:-'centralus'}

echo "Selecting subscription..."
az account set -s $subscriptionId > /dev/null

if [ $? -ne 0 ]
then
    exit $?
fi

deployDateTime=`date +%Y%m%d-%H%M%S`

deploymentName='azureDeploy'-$deployDateTime

echo "Registering required resource providers for sample..."
for i in "microsoft.storage" \
            "microsoft.web" \
            "microsoft.servicebus" \
            "microsoft.kusto" \
            "microsoft.eventhub" \
            "microsoft.eventgrid"
do
    az provider register -n $i
done

echo "Creating resource group if needed..."
az group create -n $resourceGroupName --location $location > /dev/null

echo -e "\nDeploying Azure resources..."
echo "This step usually takes 10-15 minutes to complete, standby!"

initialDeployResult=`az deployment group create -n $deploymentName -g $resourceGroupName --template-file main.bicep`

if [ $? -eq 0 ]
then

echo 'Building & Deploying Function Apps ...'
pushd ./Producer > /dev/null
appName=`echo $initialDeployResult | jq -r '.properties.outputs["producerApp"].value'`
func3 azure functionapp publish $appName --csharp
popd > /dev/null
pushd ./Consumer > /dev/null
appName=`echo $initialDeployResult | jq -r '.properties.outputs["consumerApp"].value'`
func3 azure functionapp publish $appName --csharp
popd > /dev/null
pushd ./Consumer.net5 > /dev/null
appName=`echo $initialDeployResult | jq -r '.properties.outputs["consumerAppv5"].value'`
func3 azure functionapp publish $appName --csharp
popd > /dev/null
pushd ./Consumer.net6 > /dev/null
appName=`echo $initialDeployResult | jq -r '.properties.outputs["consumerAppv6"].value'`
func azure functionapp publish $appName --csharp
popd > /dev/null

createTableRequestBody="{ 'csl':'.create table SampleDataTable (MessageProcessedTime: datetime, CloudProvider: string, TestRun: string, Trigger: string, Properties: dynamic)',	'db':'sampledata' }"
createDataMappingRequestBody="{	'csl':'.create table SampleDataTable ingestion json mapping \'DataMapping\' \'[{ \"column\":\"MessageProcessedTime\",\"path\":\"$.MessageProcessedTime\",\"datatype\":\"datetime\"},{\"column\":\"CloudProvider\",\"path\":\"$.CloudProvider\",\"datatype\":\"string\"},{\"column\":\"TestRun\",\"path\":\"$.TestRun\",\"datatype\":\"string\"},{\"column\":\"Trigger\",\"path\":\"$.Trigger\",\"datatype\":\"string\"},{\"column\":\"Properties\",\"path\":\"$.Properties\",\"datatype\":\"dynamic\"}]\'',	'db':'sampledata' }"

dexResourceUrl='https://'`echo $initialDeployResult | jq -r '.properties.outputs["dexResourceHost"].value'`

az rest -m post -u $dexResourceUrl'/v1/rest/mgmt' -b "$(echo $createTableRequestBody)" --headers "ContentType=application/json" --headers "Accept=application/json" --resource $dexResourceUrl > /dev/null
az rest -m post -u $dexResourceUrl'/v1/rest/mgmt' -b "$(echo $createDataMappingRequestBody)" --headers "ContentType=application/json" --headers "Accept=application/json" --resource $dexResourceUrl > /dev/null

echo "Setting up data ingestion from Event Hub -> Data Explorer..."
dexDeploymentName=$deploymentName'-dexdataconnection'
az deployment group create -n $dexDeploymentName -g $resourceGroupName --template-file main.dexdataconnection.bicep

eventHubProducerUrl=`echo $initialDeployResult | jq -r '.properties.outputs["eventHubProducer"].value'`
eventHubKafkaProducerUrl=`echo $initialDeployResult | jq -r '.properties.outputs["eventHubKafkaProducer"].value'`
serviceBusProducer=`echo $initialDeployResult | jq -r '.properties.outputs["serviceBusProducer"].value'`
storageQueueProducer=`echo $initialDeployResult | jq -r '.properties.outputs["storageQueueProducer"].value'`
eventGridProducer=`echo $initialDeployResult | jq -r '.properties.outputs["eventGridProducer"].value'`

echo -e "\e[32mDone!"
echo -e "\e[39mYour Producer URLs are as follows:\nEvent Hubs: "$eventHubProducerUrl"\nEvent Hubs Kafka: "$eventHubKafkaProducerUrl"\nService Bus: "$serviceBusProducer"\nStorage Queue: "$storageQueueProducer"\nEvent Grid: "$eventGridProducer"\n\nView the readme for their associated payloads."
else
echo $initialDeployResult
fi