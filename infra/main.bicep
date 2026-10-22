// One environment of the architecture doc's resource table, in South Africa North (POPIA).
// Deploy once per environment into one resource group; both environments share the App Service plan.
//
//   az deployment group create -g XBCAD -f infra/main.bicep -p env=staging sqlAdminPassword=... jwtSigningKey=...
//   az deployment group create -g XBCAD -f infra/main.bicep -p env=prod sqlAdminPassword=... jwtSigningKey=... alertEmail=...
//
// The deployer needs Owner (or User Access Administrator) on the group: it grants the apps Key Vault access.
//
// Reference: Microsoft, "What is Bicep?" Accessed: Oct. 04, 2026. [Online]. Available: https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/overview
// Reference: Microsoft, "What are Azure regions?" Accessed: Aug. 08, 2026. [Online]. Available: https://learn.microsoft.com/en-us/azure/reliability/regions-overview
// Reference: T. Fisher, "Data residency: What is it and why is it important?" IBM. Accessed: Aug. 13, 2026. [Online]. Available: https://www.ibm.com/think/insights/data-residency-why-is-it-important
// Reference: Republic of South Africa, "Protection of Personal Information Act 4 of 2013," Government Gazette, no. 37067, Cape Town, South Africa, Nov. 2013.

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
@description('Receives the availability and DTU alerts (prod only).')
param alertEmail string = ''
@description('Named developer IPs admitted to SQL. The doc requires these to be removed before go-live.')
param devIps array = []
@description('Existing vault in this resource group. Must use the Azure RBAC permission model.')
param keyVaultName string = 'XBCADKeys'

var name = 'quloobul'
var suffix = take(uniqueString(resourceGroup().id, env), 5)
var isProd = env == 'prod'
var secretsUserRole = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')

// ------------------------------------------------ Compute ------------------------------------------------
// P0v3: the cheapest tier with deployment slots, which cd-production.yml's swap needs.
// Reference: Microsoft, "Pricing - App Service for Linux." Accessed: Aug. 12, 2026. [Online]. Available: https://azure.microsoft.com/en-us/pricing/details/app-service/linux/
resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: 'asp-${name}'
  location: location
  kind: 'linux'
  sku: { name: 'P0v3' }
  properties: { reserved: true }
}

// Reference: Microsoft, "Configure an App Service app." Accessed: Oct. 04, 2026. [Online]. Available: https://learn.microsoft.com/en-us/azure/app-service/configure-common
// Reference: Microsoft, "Monitor App Service instances by using Health check." Accessed: Oct. 04, 2026. [Online]. Available: https://learn.microsoft.com/en-us/azure/app-service/monitor-instances-health-check
var siteConfig = {
  linuxFxVersion: 'DOTNETCORE|10.0'
  alwaysOn: true              // MaintenanceJobs is an in-process timer; an idle-unloaded app never sweeps
  webSocketsEnabled: true     // SignalR, when Azure SignalR Service is not in front (staging)
  healthCheckPath: '/health'
  minTlsVersion: '1.2'
  ftpsState: 'Disabled'
  http20Enabled: true
}

// Reference: Microsoft, "Use Key Vault references as app settings in Azure App Service." Accessed: Oct. 04, 2026. [Online]. Available: https://learn.microsoft.com/en-us/azure/app-service/app-service-key-vault-references
func kvRef(vault string, secret string) string => '@Microsoft.KeyVault(VaultName=${vault};SecretName=${secret})'

var webOrigin = 'https://${web.properties.defaultHostname}'
var appSettings = concat([
  { name: 'ConnectionStrings__Default', value: kvRef(kv.name, '${env}-sql-connection') }
  { name: 'ConnectionStrings__BlobStorage', value: kvRef(kv.name, '${env}-blob-connection') }
  { name: 'Jwt__SigningKey', value: kvRef(kv.name, '${env}-jwt-signing-key') }
  { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appi.properties.ConnectionString }
  { name: 'Cors__Origins__0', value: webOrigin }
  { name: 'App__BaseUrl', value: webOrigin }
  // Serves /openapi/v1.json outside development. Set to 'false' to take the contract offline.
  { name: 'OpenApi__Enabled', value: 'true' }
  { name: 'Email__ConnectionString', value: kvRef(kv.name, '${env}-acs-connection') }
  { name: 'Email__From', value: 'DoNotReply@${emailDomain.properties.fromSenderDomain}' }
], isProd ? [
  { name: 'Azure__SignalR__ConnectionString', value: kvRef(kv.name, '${env}-signalr-connection') }
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
// Reference: Microsoft, "Set Up Staging Environments - Azure App Service." Accessed: Aug. 08, 2026. [Online]. Available: https://learn.microsoft.com/en-us/azure/app-service/deploy-staging-slots
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

// Reference: Microsoft, "What is Azure Static Web Apps?" Accessed: Aug. 08, 2026. [Online]. Available: https://learn.microsoft.com/en-us/azure/static-web-apps/overview
// Reference: Microsoft, "Pricing - Static Web Apps." Accessed: Aug. 13, 2026. [Online]. Available: https://azure.microsoft.com/en-us/pricing/details/app-service/static/
resource web 'Microsoft.Web/staticSites@2023-12-01' = {
  name: 'stapp-${name}-${env}'
  location: staticWebAppLocation
  sku: { name: 'Free', tier: 'Free' }
  properties: {}
}

// ------------------------------------------------ Data ---------------------------------------------------
// Reference: Microsoft, "Security Overview - Azure SQL Database & Azure SQL Managed Instance & Azure Synapse Analytics." Accessed: Aug. 08, 2026. [Online]. Available: https://learn.microsoft.com/en-us/azure/azure-sql/database/security-overview?view=azuresql
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
// Reference: Microsoft, "DTU-Based Purchasing Model - Azure SQL Database." Accessed: Aug. 08, 2026. [Online]. Available: https://learn.microsoft.com/en-us/azure/azure-sql/database/service-tiers-dtu?view=azuresql
// Reference: Microsoft, "Serverless compute tier - Azure SQL Database." Accessed: Aug. 08, 2026. [Online]. Available: https://learn.microsoft.com/en-us/azure/azure-sql/database/serverless-tier-overview?view=azuresql
// Reference: Microsoft, "Pricing - Azure SQL Database Single Database." Accessed: Aug. 12, 2026. [Online]. Available: https://azure.microsoft.com/en-us/pricing/details/azure-sql-database/single/
resource db 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sql
  name: 'sqldb-${name}-${env}'
  location: location
  sku: { name: isProd ? 'S1' : 'S0', tier: 'Standard' }
}

// SQL admits only the App Service outbound IPs and named developer IPs; CD opens a rule per run.
// Reference: Microsoft, "Inbound and outbound IP addresses in Azure App Service." Accessed: Oct. 04, 2026. [Online]. Available: https://learn.microsoft.com/en-us/azure/app-service/overview-inbound-outbound-ips
module sqlFirewall 'sqlFirewall.bicep' = {
  name: 'sql-firewall-${env}'
  params: {
    sqlServerName: sql.name
    ips: union(split(app.properties.possibleOutboundIpAddresses, ','), devIps)
  }
}

// Reference: Microsoft, "Security recommendations for Blob storage." Accessed: Oct. 04, 2026. [Online]. Available: https://learn.microsoft.com/en-us/azure/storage/blobs/security-recommendations
// Reference: Microsoft, "Azure Blob Storage pricing." Accessed: Aug. 12, 2026. [Online]. Available: https://azure.microsoft.com/en-us/pricing/details/storage/blobs/
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
// Reference: Microsoft, "Soft delete for blobs." Accessed: Oct. 04, 2026. [Online]. Available: https://learn.microsoft.com/en-us/azure/storage/blobs/soft-delete-blob-overview
// Reference: Microsoft, "Cross-Origin Resource Sharing (CORS) support for Azure Storage." Accessed: Oct. 04, 2026. [Online]. Available: https://learn.microsoft.com/en-us/rest/api/storageservices/cross-origin-resource-sharing--cors--support-for-the-azure-storage-services
// Reference: Microsoft, "Grant limited access to data with shared access signatures (SAS)." Accessed: Aug. 08, 2026. [Online]. Available: https://learn.microsoft.com/en-us/azure/storage/common/storage-sas-overview
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
// Reference: Microsoft, "What is Azure SignalR Service?" Accessed: Oct. 04, 2026. [Online]. Available: https://learn.microsoft.com/en-us/azure/azure-signalr/signalr-overview
// Reference: Microsoft, "Azure SignalR Service Pricing." Accessed: Aug. 12, 2026. [Online]. Available: https://azure.microsoft.com/en-us/pricing/details/signalr-service/
resource signalR 'Microsoft.SignalRService/signalR@2024-03-01' = if (isProd) {
  name: 'sigr-${name}-${suffix}'
  location: location
  kind: 'SignalR'
  sku: { name: 'Free_F1', capacity: 1 }
  properties: {
    features: [{ flag: 'ServiceMode', value: 'Default' }]
  }
}

// Email: an Azure-managed sender domain (<guid>.azurecomm.net) needs no DNS. Once the NPO owns a
// domain, add it as a CustomerManaged domain here, link it, and point Email__From at it.
// Reference: Microsoft, "Email in Azure Communication Services." Accessed: Oct. 04, 2026. [Online]. Available: https://learn.microsoft.com/en-us/azure/communication-services/concepts/email/email-overview
// Reference: Microsoft, "Azure Communication Services pricing." Accessed: Aug. 12, 2026. [Online]. Available: https://azure.microsoft.com/en-us/pricing/details/communication-services/
resource emailService 'Microsoft.Communication/emailServices@2023-04-01' = {
  name: 'ecs-${name}-${env}'
  location: 'global'
  properties: { dataLocation: 'Africa' }   // POPIA: data at rest stays in South Africa
}

resource emailDomain 'Microsoft.Communication/emailServices/domains@2023-04-01' = {
  parent: emailService
  name: 'AzureManagedDomain'
  location: 'global'
  properties: { domainManagement: 'AzureManaged', userEngagementTracking: 'Disabled' }
}

resource acs 'Microsoft.Communication/communicationServices@2023-04-01' = {
  name: 'acs-${name}-${env}-${suffix}'
  location: 'global'
  properties: { dataLocation: 'Africa', linkedDomains: [emailDomain.id] }
}

// ------------------------------------------------ Secrets ------------------------------------------------
// Read by the apps' system-assigned identities through Key Vault references: no key in any app setting.
// Shared by both environments, hence the env prefix on every secret name.
// Reference: Microsoft, "Authenticate to Azure Key Vault." Accessed: Aug. 08, 2026. [Online]. Available: https://learn.microsoft.com/en-us/azure/key-vault/general/authentication
// Reference: Microsoft, "Pricing - Key Vault." Accessed: Aug. 12, 2026. [Online]. Available: https://azure.microsoft.com/en-us/pricing/details/key-vault/
resource kv 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

resource sqlSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: kv
  name: '${env}-sql-connection'
  properties: { value: 'Server=tcp:${sql.properties.fullyQualifiedDomainName},1433;Database=${db.name};User ID=${sqlAdminLogin};Password=${sqlAdminPassword};Encrypt=True;MultipleActiveResultSets=True' }
}

// An account-key connection string: BlobStorageService signs its SAS with the shared key.
resource blobSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: kv
  name: '${env}-blob-connection'
  properties: { value: 'DefaultEndpointsProtocol=https;AccountName=${storage.name};AccountKey=${storage.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}' }
}

resource jwtSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: kv
  name: '${env}-jwt-signing-key'
  properties: { value: jwtSigningKey }
}

resource acsSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: kv
  name: '${env}-acs-connection'
  properties: { value: acs.listKeys().primaryConnectionString }
}

resource signalRSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (isProd) {
  parent: kv
  name: '${env}-signalr-connection'
  properties: { value: signalR!.listKeys().primaryConnectionString }
}

// Granted per secret rather than on the shared vault, so staging cannot read prod's secrets.
// Reference: Microsoft, "Provide access to Key Vault keys, certificates, and secrets with Azure role-based access control." Accessed: Oct. 04, 2026. [Online]. Available: https://learn.microsoft.com/en-us/azure/key-vault/general/rbac-guide
var secretNames = concat(['sql-connection', 'blob-connection', 'jwt-signing-key', 'acs-connection'], isProd ? ['signalr-connection'] : [])
resource envSecrets 'Microsoft.KeyVault/vaults/secrets@2023-07-01' existing = [for s in secretNames: {
  parent: kv
  name: '${env}-${s}'
}]

resource appCanRead 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for (s, i) in secretNames: {
  name: guid(kv.id, s, app.id, secretsUserRole)
  scope: envSecrets[i]
  properties: { roleDefinitionId: secretsUserRole, principalId: app.identity.principalId, principalType: 'ServicePrincipal' }
  dependsOn: [sqlSecret, blobSecret, jwtSecret, acsSecret, signalRSecret]
}]

resource slotCanRead 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for (s, i) in secretNames: if (isProd) {
  name: guid(kv.id, s, '${app.id}/staging', secretsUserRole)
  scope: envSecrets[i]
  properties: { roleDefinitionId: secretsUserRole, principalId: slot!.identity.principalId, principalType: 'ServicePrincipal' }
  dependsOn: [sqlSecret, blobSecret, jwtSecret, acsSecret, signalRSecret]
}]

// ------------------------------------------------ Monitoring ---------------------------------------------
// Reference: Microsoft, "Azure App Service Quotas and Metrics." Accessed: Aug. 08, 2026. [Online]. Available: https://learn.microsoft.com/en-us/azure/app-service/web-sites-monitor
// Reference: Microsoft, "Application Insights overview." Accessed: Oct. 04, 2026. [Online]. Available: https://learn.microsoft.com/en-us/azure/azure-monitor/app/app-insights-overview
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
// Reference: Microsoft, "Application Insights availability tests." Accessed: Oct. 04, 2026. [Online]. Available: https://learn.microsoft.com/en-us/azure/azure-monitor/app/availability
// Reference: Microsoft, "Understand how metric alerts work in Azure Monitor." Accessed: Oct. 04, 2026. [Online]. Available: https://learn.microsoft.com/en-us/azure/azure-monitor/alerts/alerts-metric-overview
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
// Reference: GitHub, "GitHub Actions documentation." Accessed: Aug. 08, 2026. [Online]. Available: https://docs.github.com/en/actions
output AZURE_WEBAPP_NAME string = app.name
output AZURE_SQL_SERVER string = sql.name
output AZURE_STATIC_WEB_APP string = web.name
output sqlHost string = sql.properties.fullyQualifiedDomainName
output database string = db.name
output webUrl string = webOrigin
