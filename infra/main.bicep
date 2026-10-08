// Open Work - Azure infrastructure
//
// Provisions everything needed to run the Open Work container as a single
// Azure Container App, pulling the image straight from GHCR (where CI
// already pushes it - see .github/workflows/build-and-publish.yml), with
// persistent state on an Azure Files share mounted at /data (the same
// volume the Dockerfile expects).
//
// Deploy into a dedicated, empty resource group, e.g.:
//   az group create -n rg-open-work -l uksouth
//   az deployment group create -g rg-open-work -f infra/main.bicep \
//       -p namePrefix=openwork vapidSubject=mailto:you@example.com
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

@description('Size, in GiB, of the Azure Files share backing /data.')
param dataShareQuotaGiB int = 5

var sanitizedPrefix = toLower(namePrefix)
var shortPrefix = take(sanitizedPrefix, 9)
var storageAccountName = '${shortPrefix}st${uniqueString(resourceGroup().id)}'
var logAnalyticsName = '${sanitizedPrefix}-logs'
var containerAppEnvName = '${sanitizedPrefix}-env'
var containerAppName = '${sanitizedPrefix}-app'
var dataShareName = 'openwork-data'
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

resource storage 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
  }
}

resource fileServices 'Microsoft.Storage/storageAccounts/fileServices@2023-01-01' = {
  parent: storage
  name: 'default'
}

resource dataShare 'Microsoft.Storage/storageAccounts/fileServices/shares@2023-01-01' = {
  parent: fileServices
  name: dataShareName
  properties: {
    shareQuota: dataShareQuotaGiB
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

resource envStorage 'Microsoft.App/managedEnvironments/storages@2024-03-01' = {
  parent: containerAppEnv
  name: 'openwork-data-storage'
  properties: {
    azureFile: {
      accountName: storage.name
      accountKey: storage.listKeys().keys[0].value
      shareName: dataShareName
      accessMode: 'ReadWrite'
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
      secrets: usesPrivateGhcr ? [
        {
          name: 'ghcr-token'
          value: ghcrToken
        }
      ] : []
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
              // NOTE: this tenant's policy forces allowSharedKeyAccess=false and
              // publicNetworkAccess=Disabled on storage accounts, which breaks Azure
              // Container Apps' AzureFile volume mount (it only supports account-key
              // SMB auth, and the storage account's public endpoint is unreachable).
              // Falling back to the container's own ephemeral filesystem for now -
              // data does NOT survive restarts/revisions. See DEPLOYMENT.md for the
              // real fix (Azure SQL/Postgres with managed-identity auth, no file mount
              // needed).
              value: 'Data Source=/data/openwork.db'
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
