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
    # Optional, Azure region to which to deploy all resources. Defaults to West US 2
    [string]$region = "centralus"
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

# Register required RPs
Write-Host "Registering resource providers..."
foreach ($resourceProvider in @("microsoft.storage", "microsoft.web", "microsoft.servicebus", "microsoft.kusto", "microsoft.eventhub")) {
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
$initialDeployResult = New-AzResourceGroupDeployment -ResourceGroupName $resourceGroupName -TemplateFile "azuredeploy.json"

$dexbaseUrl = $initialDeployResult.Outputs.Item("dexbaseUrl").Value
$queriesUrl = "$($dexbaseUrl)?query=H4sIAAAAAAAAA4VRwWrDMAy9F/oPwgyyQUjvuW7XQtlyGztoifA8YsfYyqCU/nvVum2csq0XIz3pPUtPVRsImYDxsyd4Q+t7ekHG5pQ/rilG1LQJQysRdY2xVEMnDJaohOd+GDup/piOQg2Rg3G6hIYiv44uA4LROu8QjqfAhqLIbR1a0z7BagXXcYImTlMtF8tF9e+YoigfmsHBd5THovcCQXHsWKekgOJ9B6od+tE6VavfFlOl8shfUn2o/qjL5shbT9JzMUHty92kOzMkF7wtZErJk7nO2cFcYYLucZPZM+4VusOd7pLTZ2juQbqc2n8U2fGODZcjHADkwjrlYAIAAA=="

$eventHubProducerUrl = $initialDeployResult.Outputs.Item("eventHubProducer").Value
$serviceBusProducer = $initialDeployResult.Outputs.Item("serviceBusProducer").Value
$storageQueueProducer = $initialDeployResult.Outputs.Item("storageQueueProducer").Value
$storageAccount = $initialDeployResult.Outputs.Item("storageAccountName").Value

# Storage queues aren't able to be created by ARM templates (yet) - create via AzPosh
Get-AzStorageAccount -Name $storageAccount -ResourceGroupName $resourceGroupName | New-AzStorageQueue -Name sample >$null

Start-Process $queriesUrl

Write-Host "A web page has been opened for you. Please run the queries in order by selecting each and clicking the 'Run' button. This will set up the new Azure Data Explorer instance for ingestion.`nWhen finished, please press the <Enter> key..." -ForegroundColor Yellow

$null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown');

Write-Host "Setting up data ingestion from Event Hub -> Data Explorer..."
New-AzResourceGroupDeployment -ResourceGroupName $resourceGroupName -TemplateFile "azuredeploy.dexdataconnection.json" >$null

Write-Host "Done!" -ForegroundColor Green
Write-Host "Your Producer URLs are as follows:`nEvent Hubs: $($eventHubProducerUrl)`nService Bus: $($serviceBusProducer)`nStorage Queue: $($storageQueueProducer)`n`nView the readme for their associated payloads."