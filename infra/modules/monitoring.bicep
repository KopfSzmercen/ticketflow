@description('Base name used when composing all resource names.')
param appName string

@description('Short environment name, e.g. dev or prod.')
param environment string

@description('Azure region for monitoring resources.')
param location string

@description('Retention period in days for Log Analytics workspace data.')
@minValue(30)
@maxValue(730)
param retentionInDays int

@description('Daily ingestion cap for Application Insights in GB.')
@minValue(1)
@maxValue(100)
param dailyCapGb int

var logAnalyticsWorkspaceName = 'log-${appName}-${environment}'
var applicationInsightsName = 'appi-${appName}-${environment}'

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsWorkspaceName
  location: location
  tags: {
    app: appName
    environment: environment
  }
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: retentionInDays
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: applicationInsightsName
  location: location
  kind: 'web'
  tags: {
    app: appName
    environment: environment
  }
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspace.id
    Flow_Type: 'Bluefield'
    Request_Source: 'rest'
    IngestionMode: 'LogAnalytics'
    DisableLocalAuth: true
    RetentionInDays: retentionInDays
  }
}

resource applicationInsightsPricingPlan 'microsoft.insights/components/pricingPlans@2017-10-01' = {
  parent: applicationInsights
  name: 'current'
  properties: {
    planType: 'Basic'
    cap: dailyCapGb
    stopSendNotificationWhenHitCap: false
    stopSendNotificationWhenHitThreshold: false
  }
}

@description('Application Insights connection string for ingestion from Functions runtime.')
output applicationInsightsConnectionString string = applicationInsights.properties.ConnectionString

@description('Application Insights component name.')
output applicationInsightsName string = applicationInsights.name

@description('Log Analytics workspace name.')
output logAnalyticsWorkspaceName string = logAnalyticsWorkspace.name
