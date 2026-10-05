// A module only so it can loop over the App Service outbound IPs, which are known at deploy time, not compile time.
// Incremental deployments never remove rules: delete a developer IP's rule by hand when it leaves devIps.
// Reference: Microsoft, "Iterative loops in Bicep." Accessed: Oct. 04, 2026. [Online]. Available: https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/loops
// Reference: Microsoft, "IP firewall rules - Azure SQL Database." Accessed: Oct. 04, 2026. [Online]. Available: https://learn.microsoft.com/en-us/azure/azure-sql/database/firewall-configure
param sqlServerName string
param ips array

resource sql 'Microsoft.Sql/servers@2023-08-01-preview' existing = {
  name: sqlServerName
}

resource rules 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = [for ip in ips: {
  parent: sql
  name: 'ip-${replace(ip, '.', '-')}'
  properties: { startIpAddress: ip, endIpAddress: ip }
}]
