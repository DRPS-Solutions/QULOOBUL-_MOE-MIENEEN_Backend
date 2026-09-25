// One environment of the architecture doc's resource table, in South Africa North (POPIA).
// Deploy once per environment into one resource group; both environments share the App Service plan.
//
//   az group create -n rg-quloobul -l southafricanorth
//   az deployment group create -g rg-quloobul -f infra/main.bicep -p env=staging sqlAdminPassword=... jwtSigningKey=...
//   az deployment group create -g rg-quloobul -f infra/main.bicep -p env=prod sqlAdminPassword=... jwtSigningKey=... alertEmail=...
//
// The deployer needs Owner (or User Access Administrator) on the group: it grants the apps Key Vault access.

@allowed(['staging', 'prod'])
param env string
param location string = 'southafricanorth'
@description('Static Web Apps is not offered in South Africa North. It serves only the public WASM bundle, never personal data.')
param staticWebAppLocation string = 'westeurope'
param sqlAdminLogin string = 'sqladmin'
@secure()
param sqlAdminPassword string
@secure()
@minLength(32)
param jwtSigningKey string
@secure()
@description('Empty keeps notifications in-app only (EmailService is inert without a key).')
param resendApiKey string = ''
@description('Receives the availability and DTU alerts (prod only).')
param alertEmail string = ''
@description('Named developer IPs admitted to SQL. The doc requires these to be removed before go-live.')
param devIps array = []

var name = 'quloobul'
var suffix = take(uniqueString(resourceGroup().id, env), 5)
var isProd = env == 'prod'
var secretsUserRole = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')

// ------------------------------------------------ Compute ------------------------------------------------
// P0v3: the cheapest tier with deployment slots, which cd-production.yml's swap needs.
resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: 'asp-${name}'
  location: location
  kind: 'linux'
  sku: { name: 'P0v3' }
  properties: { reserved: true }
}

var siteConfig = {
  linuxFxVersion: 'DOTNETCORE|10.0'
  alwaysOn: true              // MaintenanceJobs is an in-process timer; an idle-unloaded app never sweeps
  webSocketsEnabled: true     // SignalR, when Azure SignalR Service is not in front (staging)
  healthCheckPath: '/health'
  minTlsVersion: '1.2'
  ftpsState: 'Disabled'
  http20Enabled: true
}

func kvRef(vault string, secret string) string => '@Microsoft.KeyVault(VaultName=${vault};SecretName=${secret})'

var webOrigin = 'https://${web.properties.defaultHostname}'
var appSettings = concat([
  { name: 'ConnectionStrings__Default', value: kvRef(kv.name, 'sql-connection') }
  { name: 'ConnectionStrings__BlobStorage', value: kvRef(kv.name, 'blob-connection') }
  { name: 'Jwt__SigningKey', value: kvRef(kv.name, 'jwt-signing-key') }
  { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appi.properties.ConnectionString }
  { name: 'Cors__Origins__0', value: webOrigin }
  { name: 'App__BaseUrl', value: webOrigin }
], empty(resendApiKey) ? [] : [
  { name: 'Email__ResendApiKey', value: kvRef(kv.name, 'resend-api-key') }
], isProd ? [
  { name: 'Azure__SignalR__ConnectionString', value: kvRef(kv.name, 'signalr-connection') }
] : [])

resource app 'Microsoft.Web/sites@2023-12-01' = {
  name: 'app-${name}-api-${env}-${suffix}'
  location: location
  kind: 'app,linux'
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: union(siteConfig, {
      appSettings: concat(appSettings, [{ name: 'Jobs__Enabled', value: 'true' }])
    })
  }
}

// Production releases land here, get smoke-tested, then swap. Staging deploys straight to its app.
resource slot 'Microsoft.Web/sites/slots@2023-12-01' = if (isProd) {
  parent: app
  name: 'staging'
  location: location
  kind: 'app,linux'
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: union(siteConfig, {
      appSettings: concat(appSettings, [{ name: 'Jobs__Enabled', value: 'false' }])
    })
  }
}

// Sticky: the background sweep stays with the production slot across swaps (Program.cs).
resource sticky 'Microsoft.Web/sites/config@2023-12-01' = if (isProd) {
  parent: app
  name: 'slotConfigNames'
  properties: { appSettingNames: ['Jobs__Enabled'] }
}

resource web 'Microsoft.Web/staticSites@2023-12-01' = {
  name: 'stapp-${name}-${env}'
  location: staticWebAppLocation
  sku: { name: 'Free', tier: 'Free' }
  properties: {}
}

// ------------------------------------------------ Data ---------------------------------------------------
resource sql 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: 'sql-${name}-${env}-${suffix}'
  location: location
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    minimalTlsVersion: '1.2'
  }
}

// Provisioned, never serverless (the doc's 24/7 requirement rules out auto-pause).
resource db 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sql
  name: 'sqldb-${name}-${env}'
  location: location
  sku: { name: isProd ? 'S1' : 'S0', tier: 'Standard' }
}

// SQL admits only the App Service outbound IPs and named developer IPs; CD opens a rule per run.
module sqlFirewall 'sqlFirewall.bicep' = {
  name: 'sql-firewall-${env}'
  params: {
    sqlServerName: sql.name
    ips: union(split(app.properties.possibleOutboundIpAddresses, ','), devIps)
  }
}

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: 'st${name}${env}${suffix}'
  location: location
  kind: 'StorageV2'
  sku: { name: 'Standard_LRS' }
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    supportsHttpsTrafficOnly: true
  }
}

// Soft delete is the undo for FileUploadService.DeleteBlobAsync. CORS lets the browser PUT/GET
// straight to a SAS URL; the bytes never pass through the API. Containers are created by the API.
resource blobs 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storage
  name: 'default'
  properties: {
    deleteRetentionPolicy: { enabled: true, days: 30 }
    containerDeleteRetentionPolicy: { enabled: true, days: 30 }
    cors: {
      corsRules: [{
        allowedOrigins: [webOrigin]
        allowedMethods: ['GET', 'PUT', 'HEAD', 'OPTIONS']
        allowedHeaders: ['*']
        exposedHeaders: ['*']
        maxAgeInSeconds: 3600
      }]
    }
  }
}

// Free tier caps at 20 concurrent client connections; move to Standard_S1 if clients are refused.
resource signalR 'Microsoft.SignalRService/signalR@2024-03-01' = if (isProd) {
  name: 'sigr-${name}-${suffix}'
  location: location
  kind: 'SignalR'
  sku: { name: 'Free_F1', capacity: 1 }
  properties: {
    features: [{ flag: 'ServiceMode', value: 'Default' }]
  }
}

// ------------------------------------------------ Secrets ------------------------------------------------
// Read by the apps' system-assigned identities through Key Vault references: no key in any app setting.
resource kv 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: 'kv${name}${env}${suffix}'
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: { family: 'A', name: 'standard' }
    enableRbacAuthorization: true
  }
}

resource sqlSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: kv
  name: 'sql-connection'
  properties: { value: 'Server=tcp:${sql.properties.fullyQualifiedDomainName},1433;Database=${db.name};User ID=${sqlAdminLogin};Password=${sqlAdminPassword};Encrypt=True;MultipleActiveResultSets=True' }
}

// An account-key connection string: BlobStorageService signs its SAS with the shared key.
resource blobSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: kv
  name: 'blob-connection'
  properties: { value: 'DefaultEndpointsProtocol=https;AccountName=${storage.name};AccountKey=${storage.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}' }
}

resource jwtSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: kv
  name: 'jwt-signing-key'
  properties: { value: jwtSigningKey }
}

resource resendSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (!empty(resendApiKey)) {
  parent: kv
  name: 'resend-api-key'
  properties: { value: resendApiKey }
}

resource signalRSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (isProd) {
  parent: kv
  name: 'signalr-connection'
  properties: { value: signalR!.listKeys().primaryConnectionString }
}

resource appCanRead 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(kv.id, app.id, secretsUserRole)
  scope: kv
  properties: { roleDefinitionId: secretsUserRole, principalId: app.identity.principalId, principalType: 'ServicePrincipal' }
}

resource slotCanRead 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (isProd) {
  name: guid(kv.id, '${app.id}/staging', secretsUserRole)
  scope: kv
  properties: { roleDefinitionId: secretsUserRole, principalId: slot!.identity.principalId, principalType: 'ServicePrincipal' }
}

// ------------------------------------------------ Monitoring ---------------------------------------------
resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'log-${name}-${env}'
  location: location
  properties: { sku: { name: 'PerGB2018' }, retentionInDays: 30 }
}

resource appi 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-${name}-${env}'
  location: location
  kind: 'web'
  properties: { Application_Type: 'web', WorkspaceResourceId: logs.id }
}

// The doc's 24/7 requirement: a /health availability test, plus the 80% DTU scale-up trigger.
resource ping 'Microsoft.Insights/webtests@2022-06-15' = if (isProd) {
  name: 'health-${name}-${env}'
  location: location
  tags: { 'hidden-link:${appi.id}': 'Resource' }
  kind: 'standard'
  properties: {
    SyntheticMonitorId: 'health-${name}-${env}'
    Name: 'API /health'
    Kind: 'standard'
    Enabled: true
    Frequency: 300
    Timeout: 30
    RetryEnabled: true
    Locations: [{ Id: 'emea-nl-ams-azr' }, { Id: 'emea-gb-db3-azr' }, { Id: 'emea-fr-pra-edge' }]
    Request: { RequestUrl: 'https://${app.properties.defaultHostName}/health', HttpVerb: 'GET' }
    ValidationRules: { ExpectedHttpStatusCode: 200, SSLCheck: true, SSLCertRemainingLifetimeCheck: 7 }
  }
}

resource team 'Microsoft.Insights/actionGroups@2023-01-01' = if (isProd) {
  name: 'ag-${name}-${env}'
  location: 'global'
  properties: {
    groupShortName: name
    enabled: true
    emailReceivers: [{ name: 'team', emailAddress: alertEmail, useCommonAlertSchema: true }]
  }
}

resource apiDown 'Microsoft.Insights/metricAlerts@2018-03-01' = if (isProd) {
  name: 'alert-${name}-api-down'
  location: 'global'
  properties: {
    severity: 1
    enabled: true
    scopes: [ping!.id, appi.id]
    evaluationFrequency: 'PT1M'
    windowSize: 'PT5M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.WebtestLocationAvailabilityCriteria'
      webTestId: ping!.id
      componentId: appi.id
      failedLocationCount: 2
    }
    actions: [{ actionGroupId: team!.id }]
  }
}

resource dtuHigh 'Microsoft.Insights/metricAlerts@2018-03-01' = if (isProd) {
  name: 'alert-${name}-dtu-80'
  location: 'global'
  properties: {
    severity: 2
    enabled: true
    scopes: [db.id]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [{
        criterionType: 'StaticThresholdCriterion'
        name: 'dtu'
        metricName: 'dtu_consumption_percent'
        operator: 'GreaterThan'
        threshold: 80
        timeAggregation: 'Average'
      }]
    }
    actions: [{ actionGroupId: team!.id }]
  }
}

// ------------------------------------------------ For GitHub ---------------------------------------------
output AZURE_WEBAPP_NAME string = app.name
output AZURE_SQL_SERVER string = sql.name
output AZURE_STATIC_WEB_APP string = web.name
output sqlHost string = sql.properties.fullyQualifiedDomainName
output database string = db.name
output webUrl string = webOrigin
