<#
 .SYNOPSIS
    Deploys the Durable Functions Producer/Consumer template to Azure

 .DESCRIPTION
    Deploys an Azure Resource Manager template for 2 Azure Functions, related Storage, 2 Event Hubs, 1 Service Bus queue, and 1 Azure Data Explorer cluster + database
#>

param(
    # The subscription id where the template will be deployed.
    [Parameter(Mandatory = $True)]
    [string]$subscriptionId,
    # The resource group in to which to deploy all the resources
    [Parameter(Mandatory = $True)]
    [string]$resourceGroupName,
    # Optional, Azure region to which to deploy all resources. Defaults to Central US.
    [string]$region = "centralus",
    # Optional, name for the deployment. If not specified, deployment name will be "azuredeploy-yyyyMMdd-hhmmss" (for example, azuredeploy-20190724-083224).  Deployment to set up Azure Data Explorer will have "-dexdataconnection" appended to the base name.
    [string]$deploymentName
)

$currentSubscription = Get-AzSubscription -SubscriptionId $subscriptionId -ErrorAction SilentlyContinue
if (!$currentSubscription) {
    Write-Host "Logging in..."
    Connect-AzAccount >$null
}

$ErrorActionPreference = "Stop"

# select subscription
Write-Host "Getting subscription..."
Select-AzSubscription -Subscription $subscriptionId >$null

if (!$deploymentName) {
    $deploymentName = "azuredeploy"
    $dexDeploymentName = $deploymentName + "-dexdataconnection"

    $deployDateTime = (Get-Date).ToString("yyyyMMdd-hhmmss")
    
    # Add a datetime formatted string to the end to provide some uniqueness to the deployment name.
    $deploymentName = $deploymentName + "-" + $deployDateTime
    $dexDeploymentName = $dexDeploymentName + "-" + $deployDateTime
}
else {
     $dexDeploymentName = $deploymentName + "-dexdataconnection"
}

# Register required RPs
Write-Host "Registering resource providers..."
foreach ($resourceProvider in @("microsoft.storage", "microsoft.web", "microsoft.servicebus", "microsoft.kusto", "microsoft.eventhub", "microsoft.eventgrid")) {
    Register-AzResourceProvider -ProviderNamespace $resourceProvider >$null
}

#Create or check for existing resource group
$resourceGroup = Get-AzResourceGroup -Name $resourceGroupName -ErrorAction SilentlyContinue
if (!$resourceGroup) {
    if (!$region) {
        Write-Host "Resource group '$resourceGroupName' does not exist. To create a new resource group, please enter a location."
        $region = Read-Host "region"
    }
    Write-Host "Creating resource group '$resourceGroupName' in location '$region'"
    New-AzResourceGroup -Name $resourceGroupName -Location $region >$null
}
else {
    Write-Host "Using existing resource group '$resourceGroupName'"
}

# Start the deployment
Write-Host "Deploying Azure resources...`nThis step usually takes 10-15 minutes to complete, standby!"
$initialDeployResult = New-AzResourceGroupDeployment -ResourceGroupName $resourceGroupName -TemplateFile "azuredeploy.json" -Name $deploymentName

$dexbaseUrl = $initialDeployResult.Outputs.Item("dexbaseUrl").Value
$queriesUrl = "$($dexbaseUrl)?query=H4sIAAAAAAAAA4VRwWrDMAy9F/oPwgyyQUjvuW7XQtlyGztoifA8YsfYyqCU/nvVum2csq0XIz3pPUtPVRsImYDxsyd4Q+t7ekHG5pQ/rilG1LQJQysRdY2xVEMnDJaohOd+GDup/piOQg2Rg3G6hIYiv44uA4LROu8QjqfAhqLIbR1a0z7BagXXcYImTlMtF8tF9e+YoigfmsHBd5THovcCQXHsWKekgOJ9B6od+tE6VavfFlOl8shfUn2o/qjL5shbT9JzMUHty92kOzMkF7wtZErJk7nO2cFcYYLucZPZM+4VusOd7pLTZ2juQbqc2n8U2fGODZcjHADkwjrlYAIAAA=="

$dexResourceHost = $initialDeployResult.Outputs.Item("dexResourceHost").Value
$dexResourceUrl = "https://$dexResourceHost"

$eventHubProducerUrl = $initialDeployResult.Outputs.Item("eventHubProducer").Value
$eventHubKafkaProducerUrl = $initialDeployResult.Outputs.Item("eventHubKafkaProducer").Value
$serviceBusProducer = $initialDeployResult.Outputs.Item("serviceBusProducer").Value
$storageQueueProducer = $initialDeployResult.Outputs.Item("storageQueueProducer").Value
$eventGridProducer = $initialDeployResult.Outputs.Item("eventGridProducer").Value
$storageAccount = $initialDeployResult.Outputs.Item("storageAccountName").Value

# Storage queues aren't able to be created by ARM templates (yet) - create via AzPosh
Get-AzStorageAccount -Name $storageAccount -ResourceGroupName $resourceGroupName | New-AzStorageQueue -Name sample >$null

# Get auth token to execute Kusto queries against the Data Explorer instance to create table & data mapping
import-module az
$context = [Microsoft.Azure.Commands.Common.Authentication.Abstractions.AzureRmProfileProvider]::Instance.Profile.DefaultContext
$token = [Microsoft.Azure.Commands.Common.Authentication.AzureSession]::Instance.AuthenticationFactory.Authenticate($context.Account, $context.Environment, $context.Tenant.Id.ToString(), $null, [Microsoft.Azure.Commands.Common.Authentication.ShowDialog]::Never, $null, $dexResourceUrl).AccessToken

# Call Kusto endpoint to run queries
$createTableRequestBody = @{
	csl = ".create table SampleDataTable (MessageProcessedTime: datetime, CloudProvider: string, TestRun: string, Trigger: string, Properties: dynamic)"
	db = "sampledata"
}
$createDataMappingRequestBody = @{
	csl ='.create table SampleDataTable ingestion json mapping ''DataMapping'' ''[{ "column":"MessageProcessedTime","path":"$.MessageProcessedTime","datatype":"datetime"},{"column":"CloudProvider","path":"$.CloudProvider","datatype":"string"},{"column":"TestRun","path":"$.TestRun","datatype":"string"},{"column":"Trigger","path":"$.Trigger","datatype":"string"},{"column":"Properties","path":"$.Properties","datatype":"dynamic"}]'''
	db = "sampledata"
}
$headers = @{
	Accept = "application/json"
	Authorization = "Bearer $token"
	Host = $dexResourceHost
}

Invoke-RestMethod -Method Post -Uri "$dexResourceUrl/v1/rest/mgmt" -Body (ConvertTo-Json $createTableRequestBody) -ContentType "application/json" -Headers $headers >$null
Invoke-RestMethod -Method Post -Uri "$dexResourceUrl/v1/rest/mgmt" -Body (ConvertTo-Json $createDataMappingRequestBody) -ContentType "application/json" -Headers $headers >$null

Write-Host "Setting up data ingestion from Event Hub -> Data Explorer..."
New-AzResourceGroupDeployment -ResourceGroupName $resourceGroupName -TemplateFile "azuredeploy.dexdataconnection.json" -Name $dexDeploymentName >$null

Write-Host "Adding Event Grid subscriptions..."
New-AzEventGridSubscription -EventSubscriptionName "eg$($resourceGroupName)" -Endpoint l

Write-Host "Done!" -ForegroundColor Green
Write-Host "Your Producer URLs are as follows:`nEvent Hubs: $($eventHubProducerUrl)`nEvent Hubs Kafka: $($eventHubKafkaProducerUrl)`nService Bus: $($serviceBusProducer)`nStorage Queue: $($storageQueueProducer)`nEvent Grid: $($eventGridProducer)`n`nView the readme for their associated payloads."