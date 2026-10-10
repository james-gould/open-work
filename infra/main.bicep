// Open Work - Azure infrastructure
//
// Provisions everything needed to run the Open Work container as a single
// Azure Container App, pulling the image straight from GHCR (where CI
// already pushes it - see .github/workflows/build-and-publish.yml), with
// persistent state in an Azure Database for PostgreSQL Flexible Server
// (Burstable B1ms, ~$15-20/mo). The connection string (including the admin
// password) is stored as a Container App secret, never in plain env vars
// or source control.
//
// (Two earlier revisions tried other options that didn't pan out in this
// tenant: an Azure Files share mounted at /data - blocked by a policy
// forcing allowSharedKeyAccess=false/publicNetworkAccess=Disabled on
// storage accounts, which Container Apps' AzureFile volume type can't use;
// and Azure SQL Database - this subscription's offer type rejects new SQL
// Database logical servers in every region tried with
// "RegionDoesNotAllowProvisioning". PostgreSQL Flexible Server hit neither
// restriction. See DEPLOYMENT.md for the full story.)
//
// Deploy into a dedicated, empty resource group, e.g.:
//   az group create -n rg-open-work -l uksouth
//   az deployment group create -g rg-open-work -f infra/main.bicep \
//       -p namePrefix=openwork vapidSubject=mailto:you@example.com \
//       -p postgresAdminPassword=<a strong password, not committed anywhere> \
//       -p authSigningKey=<a random 32+ char string, not committed anywhere>
//
// GHCR auth: if the GHCR package is public (Settings > Package > Change
// visibility, one-time), leave ghcrUsername/ghcrToken empty and the
// Container App pulls with no credentials at all. If it stays private,
// pass ghcrUsername (your GitHub username) and ghcrToken (a PAT with
// read:packages) and the template wires up a registry secret instead.

@description('Short, globally-unique-ish prefix used to derive resource names (lowercase alphanumeric).')
@minLength(3)
@maxLength(12)
param namePrefix string = 'openwork'

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Container image to run, e.g. ghcr.io/james-gould/open-work:latest')
param containerImage string = 'ghcr.io/james-gould/open-work:latest'

@description('GitHub username for GHCR auth. Leave empty if the package is public.')
param ghcrUsername string = ''

@description('GitHub PAT (read:packages) for GHCR auth. Leave empty if the package is public.')
@secure()
param ghcrToken string = ''

@description('Subject (mailto: or https: URI) sent with Web Push VAPID claims.')
param vapidSubject string = 'mailto:admin@example.com'

@description('Admin username for the PostgreSQL Flexible Server.')
param postgresAdminLogin string = 'openworkadmin'

@description('Admin password for the PostgreSQL Flexible Server. Required - choose a strong value, pass it on the command line only (never commit it).')
@secure()
param postgresAdminPassword string

@description('Symmetric signing key (32+ chars) for the JWT bearer tokens issued by PIN auth. Required - generate a strong random value, pass it on the command line only (never commit it).')
@secure()
@minLength(32)
param authSigningKey string

var sanitizedPrefix = toLower(namePrefix)
var containerAppEnvName = '${sanitizedPrefix}-env'
var containerAppName = '${sanitizedPrefix}-app'
var logAnalyticsName = '${sanitizedPrefix}-logs'
var postgresServerName = '${sanitizedPrefix}-pg-${uniqueString(resourceGroup().id)}'
var postgresDatabaseName = '${sanitizedPrefix}db'
var usesPrivateGhcr = !empty(ghcrToken)

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

// Burstable B1ms (1 vCore, 2GiB RAM) + 32GB storage - cheapest tier that still gets
// predictable always-on performance; well within the ~$30/mo ceiling for a low-traffic
// single-user app, and simpler than serverless/auto-pause compute for a first pass.
resource postgresServer 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' = {
  name: postgresServerName
  location: location
  sku: {
    name: 'Standard_B1ms'
    tier: 'Burstable'
  }
  properties: {
    version: '16'
    administratorLogin: postgresAdminLogin
    administratorLoginPassword: postgresAdminPassword
    storage: {
      storageSizeGB: 32
    }
    backup: {
      backupRetentionDays: 7
      geoRedundantBackup: 'Disabled'
    }
    highAvailability: {
      mode: 'Disabled'
    }
    network: {
      publicNetworkAccess: 'Enabled'
    }
  }
}

// Postgres Flexible Server's documented "allow any Azure-originating connection" rule -
// Container Apps has no static egress IP without VNet integration, so this is the
// practical option for a single-container app reaching a public Postgres endpoint.
resource postgresAllowAzureServices 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2024-08-01' = {
  parent: postgresServer
  name: 'AllowAllAzureServicesAndResourcesWithinAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource postgresDatabase 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  parent: postgresServer
  name: postgresDatabaseName
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

resource containerAppEnv 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: containerAppEnvName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: containerAppName
  location: location
  properties: {
    managedEnvironmentId: containerAppEnv.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
      }
      secrets: concat(
        [
          {
            name: 'auth-signing-key'
            value: authSigningKey
          }
          {
            name: 'db-connection-string'
            value: 'Host=${postgresServer.properties.fullyQualifiedDomainName};Database=${postgresDatabaseName};Username=${postgresAdminLogin};Password=${postgresAdminPassword};SSL Mode=Require;Trust Server Certificate=true'
          }
        ],
        usesPrivateGhcr ? [
          {
            name: 'ghcr-token'
            value: ghcrToken
          }
        ] : []
      )
      registries: usesPrivateGhcr ? [
        {
          server: 'ghcr.io'
          username: ghcrUsername
          passwordSecretRef: 'ghcr-token'
        }
      ] : []
    }
    template: {
      containers: [
        {
          name: 'open-work'
          image: containerImage
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            {
              name: 'ConnectionStrings__Default'
              secretRef: 'db-connection-string'
            }
            {
              name: 'Auth__SigningKey'
              secretRef: 'auth-signing-key'
            }
            {
              name: 'Vapid__Subject'
              value: vapidSubject
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
    }
  }
}

@description('Fully-qualified domain name of the deployed Container App.')
output containerAppFqdn string = containerApp.properties.configuration.ingress.fqdn

@description('Name of the created Container App (for `az containerapp update` redeploys from CI).')
output containerAppName string = containerApp.name

@description('Name of the created Container Apps managed environment.')
output containerAppEnvironmentName string = containerAppEnv.name

@description('Fully-qualified domain name of the PostgreSQL Flexible Server.')
output postgresServerFqdn string = postgresServer.properties.fullyQualifiedDomainName

@description('Name of the PostgreSQL database.')
output postgresDatabaseName string = postgresDatabase.name
