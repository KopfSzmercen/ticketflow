using '../main.bicep'

// Dev environment — West Europe
// To add a new environment, copy this file and change the values below.
param environment = 'dev'
param location = 'polandcentral'
param appName = 'ticketflow'
param serviceBusOrderEventsTopicName = 'order-events'
param serviceBusEmailWorkerSubscriptionName = 'email-worker'
param serviceBusAnalyticsWorkerSubscriptionName = 'analytics-worker'
param serviceBusQrWorkerSubscriptionName = 'qr-worker'
param monitoringRetentionInDays = 30
param monitoringDailyCapGb = 1
param monitoringSamplingProfile = 'minimal'
param monitoringSamplingPercentage = 10
