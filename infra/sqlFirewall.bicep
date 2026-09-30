// A module only so it can loop over the App Service outbound IPs, which are known at deploy time, not compile time.
// Incremental deployments never remove rules: delete a developer IP's rule by hand when it leaves devIps.
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
