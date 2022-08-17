#!/bin/bash
set -ev
projectId=bccGraphWebjob-$RANDOM

echo $'\e[1;33m'$projectId$'\e[0m'

location="westus"
rgName=$projectId-RG

az config set defaults.location=$location defaults.group=$rgName core.output=tsv extension.use_dynamic_install=yes_without_prompt --only-show-errors

#
# Deploy Azure Resources
#

rgId=$(az group create --name $rgName --query id)

appSvcName=$projectId-AppSvcFreePlan

# "Deploy WebApp Service Plan"
az appservice plan create --name $appSvcName --sku FREE --query provisioningState --output none

webAppName=$projectId-WebApp

# "Deploy WebApp"
webAppManagedId=$(az webapp create -p $appSvcName -n $webAppName --assign-identity --scope $rgId --only-show-errors --query identity.principalId)

webAppPubIPs=$(az webapp show -n $webAppName --query possibleOutboundIpAddresses)

keyVaultName=$projectId-KV

# "Deploy KeyVault with network rule to allow access from WebApp public IP and default action Deny"
az keyvault create -n "$keyVaultName" --network-acls-ips ${webAppPubIPs//,/ } --default-action Deny --output none

# "Adding KeyVault policy to allow access from WebApp Managed Identity"
az keyvault set-policy --name $keyVaultName --object-id $webAppManagedId --secret-permissions get list --output none

# "Add KeyVault name as an environment variable to the WebApp"
az webapp config appsettings set -n $webAppName --settings KEY_VAULT_NAME=$keyVaultName --output none

adAppName=$projectId-ADApp

# "Register AD App for app-only auth of the Graph Console Application"
adAppId=$(az ad app create --display-name $adAppName --is-fallback-public-client --sign-in-audience AzureADMyOrg --query appId)

# "Add password to the AD App"
adAppPw=$(az ad app credential reset --id $adAppId --query password -o tsv --only-show-errors)

# "Add Graph API permissions to the AD App"
az ad app permission add --id $adAppId --api 00000003-0000-0000-c000-000000000000 --api-permissions 75359482-378d-4052-8f01-80520e7db3cd=Role --only-show-errors
az ad app permission add --id $adAppId --api 00000003-0000-0000-c000-000000000000 --api-permissions 01d4889c-1287-42c6-ac1f-5d1e02578ef6=Role --only-show-errors
az ad app permission add --id $adAppId --api 00000003-0000-0000-c000-000000000000 --api-permissions df021288-bdef-4463-88db-98f22de89214=Role --only-show-errors
az ad app permission add --id $adAppId --api 00000003-0000-0000-c000-000000000000 --api-permissions e2a3a72e-5f79-4c64-b1b1-878b674786c9=Role --only-show-errors
az ad app permission add --id $adAppId --api 00000003-0000-0000-c000-000000000000 --api-permissions b633e1c5-b582-4048-a93e-9f11b44c7e96=Role --only-show-errors

# "Add Service Principal to the AD App"
az ad sp create --id $adAppId --output none

# "Grant permissions to the AD App"
az ad app permission grant --id $adAppId --api 00000003-0000-0000-c000-000000000000 --scope Files.Read.All Files.ReadWrite.All Mail.ReadWrite Mail.Send User.Read.All --output none

# "Refresh login token"
tenantId=$(az login --query [].tenantId)

# "Admin-Consent permissions to the AD App"
az ad app permission admin-consent --id $adAppId --output none

localpubip=$(dig +short myip.opendns.com @resolver1.opendns.com)

# "Add network rule for local public ip"
az keyvault network-rule add --name $keyVaultName --ip-address $localpubip --output none

# "Add AD App ID and Password as secrets to the KeyVault"
az keyvault secret set --vault-name $keyVaultName --name clientId --value $adAppId --output none
az keyvault secret set --vault-name $keyVaultName --name clientSecret --value $adAppPw --output none

# "Add Tenant ID as a secret to the KeyVault"
az keyvault secret set --vault-name $keyVaultName --name tenantId --value $tenantId --output none

# "Remove network rule for local public ip"
az keyvault network-rule remove --name $keyVaultName --ip-address $localpubip --output none

#
# Deploy DevOps Project and Pipelines
#

subsId=$(az account show --query id)
spName=$projectId-sp

# "Register Azure Service Principal for the pipelines with enough rights to deploy and run the webjob"
spKey=$(az ad sp create-for-rbac --name $spName --role "Website Contributor" --scopes $rgId --only-show-errors --query password)

spClientId=$(az ad sp list --display-name $spName --query [].appId)
subsName=$(az account show --query name)

# Adjust for your enviroment
devOpsOrgUrl=https://dev.azure.com/Better-Computing-Consulting
az devops configure --defaults organization=$devOpsOrgUrl

#export AZURE_DEVOPS_EXT_GITHUB_PAT=enter-github-pat-here
export AZURE_DEVOPS_EXT_AZURE_RM_SERVICE_PRINCIPAL_KEY=$spKey

# "Create Azure DevOps project"
az devops project create --name $projectId --output none

# "Create AzureRM service endpoint"
azRMSvcId=$(az devops service-endpoint azurerm create --azure-rm-service-principal-id $spClientId \
	--azure-rm-subscription-id $subsId --azure-rm-subscription-name "$subsName" --azure-rm-tenant-id $tenantId \
	--name AzureServiceConnection --project $projectId --query id)

# "Enable AzureRM service endpoint for all pipelines"
az devops service-endpoint update --id $azRMSvcId --enable-for-all true --project $projectId --output none

# "Create GitHub service endpoint"
gitHubSvcId=$(az devops service-endpoint github create --github-url https://github.com/ --name GitHubService --project $projectId --query id)

# "Enable Github service endpoint for all pipelines"
az devops service-endpoint update --id $gitHubSvcId --enable-for-all true --project $projectId --output none

# Adjust for your enviroment
pipelinesRepo=https://github.com/Better-Computing-Consulting/microsoft-graph-api-excel-emailer-webjob

# "Create BuildDeploy Pipeline"
pipelineId=$(az pipelines create --name BuildDeployPipeline --project $projectId --repository $pipelinesRepo --branch master \
	--yml-path azure-pipelines.yml --skip-first-run true --service-connection $gitHubSvcId --only-show-errors --query id)

echo $'\e[1;33m'$devOpsOrgUrl/$projectId/_build?definitionId=$pipelineId$'\e[0m'

# "Create pipeline WebAppName variable"
az pipelines variable create --name WebAppName --value $webAppName --project $projectId --pipeline-id $pipelineId --output none
az pipelines variable create --name ResGrpName --value $rgName --project $projectId --pipeline-id $pipelineId --output none

# "Create CronRun Pipeline"
pipelineId=$(az pipelines create --name CronRunPipeline --project $projectId --repository $pipelinesRepo --branch master \
        --yml-path cron-pipeline.yml --skip-first-run true --service-connection $gitHubSvcId --only-show-errors --query id)

echo $'\e[1;33m'$devOpsOrgUrl/$projectId/_build?definitionId=$pipelineId$'\e[0m'

# "Create pipeline WebAppName and ResGrpName variables"
az pipelines variable create --name WebAppName --value $webAppName --project $projectId --pipeline-id $pipelineId --output none
az pipelines variable create --name ResGrpName --value $rgName --project $projectId --pipeline-id $pipelineId --output none
