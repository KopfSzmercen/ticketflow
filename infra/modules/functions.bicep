@description('Base name used when composing all resource names.')
param appName string

@description('Short environment name, e.g. dev or prod.')
param environment string

@description('Azure region for the Function App.')
param location string

@description('Storage account name — the Functions runtime connects via managed identity.')
param storageAccountName string

@description('Cosmos DB endpoint URL — the app connects via managed identity using DefaultAzureCredential.')
param cosmosAccountEndpoint string

@description('Primary blob endpoint of the storage account, used to build the deployment package container URL.')
param storageAccountBlobEndpoint string

@description('Service Bus namespace FQDN used by the application in cloud mode.')
param serviceBusNamespaceFqdn string

@description('Service Bus topic for order lifecycle events.')
param serviceBusOrderEventsTopicName string

@description('Service Bus subscription used by email worker.')
param serviceBusEmailWorkerSubscriptionName string

@description('Service Bus subscription used by analytics worker.')
param serviceBusAnalyticsWorkerSubscriptionName string

@description('Service Bus subscription used by QR worker.')
param serviceBusQrWorkerSubscriptionName string

@description('Application Insights connection string for telemetry ingestion.')
param applicationInsightsConnectionString string

@description('Telemetry profile (minimal for lowest cost, balanced for higher fidelity).')
@allowed([
  'minimal'
  'balanced'
])
param telemetryProfile string

@description('Initial adaptive sampling percentage used by the Functions host.')
@minValue(1)
@maxValue(100)
param telemetrySamplingPercentage int

var planName = 'asp-${appName}-${environment}'
var functionAppName = 'func-${appName}-${environment}'
// Flex Consumption requires a blob container URL for deployment package storage.
var deploymentStorageContainerUrl = '${storageAccountBlobEndpoint}deploymentpackages'
var effectiveDefaultLogLevel = telemetryProfile == 'minimal' ? 'Warning' : 'Information'
var effectiveDependencyTracking = telemetryProfile == 'minimal' ? 'false' : 'true'

// Consumption plan on Linux (F1 Free)
resource hostingPlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: planName
  location: location
  kind: 'functionapp,linux'
  sku: {
    name: 'FC1'
    tier: 'FlexConsumption'
  }
  properties: {
    reserved: true // required for Linux-based plans
  }
}

resource functionApp 'Microsoft.Web/sites@2024-11-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp,linux'
  // System-assigned managed identity — grants the app a principal that can be given
  // RBAC roles on Storage and Cosmos DB without storing any credentials.
  identity: {
    type: 'SystemAssigned'
  }
  tags: {
    app: appName
    environment: environment
  }
  properties: {
    serverFarmId: hostingPlan.id
    httpsOnly: true
    // Required for Flex Consumption (FC1) plans
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: deploymentStorageContainerUrl
          authentication: {
            type: 'SystemAssignedIdentity'
          }
        }
      }
      scaleAndConcurrency: {
        maximumInstanceCount: 40
        instanceMemoryMB: 512
      }
      runtime: {
        name: 'dotnet-isolated'
        version: '10.0'
      }
    }
    siteConfig: {
      // linuxFxVersion is not applicable for Flex Consumption — runtime is set in functionAppConfig.
      appSettings: [
        {
          // Managed-identity connection: the host looks up the account by name and
          // authenticates with the Function App's system-assigned identity.
          name: 'AzureWebJobsStorage__accountName'
          value: storageAccountName
        }
        {
          name: 'AzureWebJobsStorage__credential'
          value: 'managedidentity'
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          // Endpoint only — no key. The app must use DefaultAzureCredential in code.
          name: 'CosmosDb__AccountEndpoint'
          value: cosmosAccountEndpoint
        }
        {
          name: 'CosmosDb__AuthMode'
          value: 'ManagedIdentity'
        }
        {
          name: 'ServiceBus__FullyQualifiedNamespace'
          value: serviceBusNamespaceFqdn
        }
        {
          name: 'ServiceBus__AuthMode'
          value: 'ManagedIdentity'
        }
        {
          name: 'ServiceBus__TopicName'
          value: serviceBusOrderEventsTopicName
        }
        {
          name: 'ServiceBus__EmailSubscriptionName'
          value: serviceBusEmailWorkerSubscriptionName
        }
        {
          name: 'ServiceBus__AnalyticsSubscriptionName'
          value: serviceBusAnalyticsWorkerSubscriptionName
        }
        {
          name: 'ServiceBus__QrSubscriptionName'
          value: serviceBusQrWorkerSubscriptionName
        }
        {
          name: 'TicketStorage__AuthMode'
          value: 'ManagedIdentity'
        }
        {
          name: 'TicketStorage__AccountName'
          value: storageAccountName
        }
        {
          name: 'TicketStorage__Containers__tickets'
          value: 'tickets'
        }
        {
          // Workspace-based Application Insights ingestion (no instrumentation key).
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: applicationInsightsConnectionString
        }
        {
          // Keep invocation outcomes while reducing low-value noise by default.
          name: 'AzureFunctionsJobHost__logging__logLevel__default'
          value: effectiveDefaultLogLevel
        }
        {
          name: 'AzureFunctionsJobHost__logging__logLevel__Microsoft'
          value: effectiveDefaultLogLevel
        }
        {
          name: 'AzureFunctionsJobHost__logging__logLevel__Azure'
          value: effectiveDefaultLogLevel
        }
        {
          name: 'AzureFunctionsJobHost__logging__applicationInsights__samplingSettings__initialSamplingPercentage'
          value: string(telemetrySamplingPercentage)
        }
        {
          name: 'AzureFunctionsJobHost__logging__applicationInsights__samplingSettings__minSamplingPercentage'
          value: telemetryProfile == 'minimal' ? '5' : '10'
        }
        {
          name: 'AzureFunctionsJobHost__logging__applicationInsights__samplingSettings__maxSamplingPercentage'
          value: telemetryProfile == 'minimal' ? '20' : '50'
        }
        {
          name: 'AzureFunctionsJobHost__logging__applicationInsights__enableDependencyTracking'
          value: effectiveDependencyTracking
        }
      ]
    }
  }
}

@description('Default hostname of the deployed Function App.')
output functionAppHostname string = functionApp.properties.defaultHostName

@description('Principal ID of the Function App system-assigned managed identity.')
output principalId string = functionApp.identity.principalId
