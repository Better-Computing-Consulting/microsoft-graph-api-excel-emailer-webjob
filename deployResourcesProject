#!/bin/bash
projectId=bccGraphWebjob-$RANDOM
echo $projectId

location="westus"
rgName=$projectId-RG

az config set defaults.location=$location defaults.group=$rgName core.output=tsv extension.use_dynamic_install=yes_without_prompt --only-show-errors

#
# Deploy Azure Resources
#

rgId=$(az group create --name $rgName --query id)

appSvcName=$projectId-AppSvcFreePlan

echo "Deploy WebApp Service Plan"
az appservice plan create --name $appSvcName --sku FREE --query provisioningState --output none

webAppName=$projectId-WebApp

echo "Deploy WebApp"
webAppManagedId=$(az webapp create -p $appSvcName -n $webAppName --assign-identity --scope $rgId --only-show-errors --query identity.principalId)

webAppPubIPs=$(az webapp show -n $webAppName --query possibleOutboundIpAddresses)

keyVaultName=$projectId-KV

echo "Deploy KeyVault with network rule to allow access from WebApp public IP and default action Deny"
az keyvault create -n "$keyVaultName" --network-acls-ips ${webAppPubIPs//,/ } --default-action Deny --query provisioningState --output none

echo "Adding KeyVault policy to allow access from WebApp Managed Identity"
az keyvault set-policy --name $keyVaultName --object-id $webAppManagedId --secret-permissions get list --query provisioningState --output none

echo "Add KeyVault name as an environment variable to the WebApp"
az webapp config appsettings set -n $webAppName --settings KEY_VAULT_NAME=$keyVaultName --output none

adAppName=$projectId-ADApp
echo "Register AD App for app-only auth of the Graph Console Application"
adAppId=$(az ad app create --display-name $adAppName --is-fallback-public-client --sign-in-audience AzureADMyOrg --query appId)

echo "Add password to the AD App"
adAppPw=$(az ad app credential reset --id $adAppId --query password -o tsv --only-show-errors)

echo "Add Graph API permissions to the AD App"
az ad app permission add --id $adAppId --api 00000003-0000-0000-c000-000000000000 --api-permissions 75359482-378d-4052-8f01-80520e7db3cd=Role --only-show-errors
az ad app permission add --id $adAppId --api 00000003-0000-0000-c000-000000000000 --api-permissions 01d4889c-1287-42c6-ac1f-5d1e02578ef6=Role --only-show-errors
az ad app permission add --id $adAppId --api 00000003-0000-0000-c000-000000000000 --api-permissions df021288-bdef-4463-88db-98f22de89214=Role --only-show-errors
az ad app permission add --id $adAppId --api 00000003-0000-0000-c000-000000000000 --api-permissions e2a3a72e-5f79-4c64-b1b1-878b674786c9=Role --only-show-errors
az ad app permission add --id $adAppId --api 00000003-0000-0000-c000-000000000000 --api-permissions b633e1c5-b582-4048-a93e-9f11b44c7e96=Role --only-show-errors

echo "Add Service Principal to the AD App"
az ad sp create --id $adAppId --output none

echo "Grant permissions to the AD App"
az ad app permission grant --id $adAppId --api 00000003-0000-0000-c000-000000000000 --scope Files.Read.All Files.ReadWrite.All Mail.ReadWrite Mail.Send User.Read.All --output none

echo "Refresh login token"
tenantId=$(az login --query [].tenantId)

echo "Admin-Consent permissions to the AD App"
az ad app permission admin-consent --id $adAppId --output none

localpubip=$(dig +short myip.opendns.com @resolver1.opendns.com)

echo "Add network rule for local public ip"
az keyvault network-rule add --name $keyVaultName --ip-address $localpubip --output none

echo "Add AD App ID and Password as secrets to the KeyVault"
az keyvault secret set --vault-name $keyVaultName --name clientId --value $adAppId --output none
az keyvault secret set --vault-name $keyVaultName --name clientSecret --value $adAppPw --output none

echo "Add Tenant ID as a secret to the KeyVault"
az keyvault secret set --vault-name $keyVaultName --name tenantId --value $tenantId --output none

echo "Remove network rule for local public ip"
az keyvault network-rule remove --name $keyVaultName --ip-address $localpubip --output none

#
# Deploy DevOps Project and Pipelines
#

subsId=$(az account show --query id)
spName=$projectId-sp

echo "Register Azure Service Principal for the pipelines"
spKey=$(az ad sp create-for-rbac --name $spName --role Contributor --scopes $rgId --only-show-errors --query password)

spClientId=$(az ad sp list --display-name $spName --query [].appId)
subsName=$(az account show --query name)

# Adjust for your enviroment
devOpsOrgUrl=https://dev.azure.com/Better-Computing-Consulting
az devops configure --defaults organization=$devOpsOrgUrl

#export AZURE_DEVOPS_EXT_GITHUB_PAT=enter-github-pat-here
export AZURE_DEVOPS_EXT_AZURE_RM_SERVICE_PRINCIPAL_KEY=$spKey

echo "Create Azure DevOps project"
az devops project create --name $projectId --output none

echo "Create AzureRM service endpoint"
azRMSvcId=$(az devops service-endpoint azurerm create --azure-rm-service-principal-id $spClientId \
	--azure-rm-subscription-id $subsId --azure-rm-subscription-name "$subsName" --azure-rm-tenant-id $tenantId \
	--name AzureServiceConnection --project $projectId --query  id)

echo "Enable AzureRM service endpoint for all pipelines"
az devops service-endpoint update --id $azRMSvcId --enable-for-all true --project $projectId --output none

echo "Create GitHub service endpoint"
gitHubSvcId=$(az devops service-endpoint github create --github-url https://github.com/ --name GitHubService --project $projectId --query id)

echo "Enable Github service endpoint for all pipelines"
az devops service-endpoint update --id $gitHubSvcId --enable-for-all true --project $projectId --output none

# Adjust for your enviroment
pipelinesRepo=https://github.com/Better-Computing-Consulting/microsoft-graph-api-excel-emailer-webjob

echo "Create BuildDeploy Pipeline"
pipelineId=$(az pipelines create --name BuildDeployPipeline --project $projectId --repository $pipelinesRepo --branch master \
	--yml-path azure-pipelines.yml --skip-first-run true --service-connection $gitHubSvcId --only-show-errors --query id)

echo "$devOpsOrgUrl/$projectId/_build?definitionId=$pipelineId"

echo "Create pipeline WebAppName variable"
az pipelines variable create --name WebAppName --value $webAppName --project $projectId --pipeline-id $pipelineId --output none

echo "Create CronRun Pipeline"
pipelineId=$(az pipelines create --name CronRunPipeline --project $projectId --repository $pipelinesRepo --branch master \
        --yml-path cron-pipeline.yml --skip-first-run true --service-connection $gitHubSvcId --only-show-errors --query id)

echo "$devOpsOrgUrl/$projectId/_build?definitionId=$pipelineId"

echo "Create pipeline WebAppName and ResGrpName variables"
az pipelines variable create --name WebAppName --value $webAppName --project $projectId --pipeline-id $pipelineId --output none
az pipelines variable create --name ResGrpName --value $rgName --project $projectId --pipeline-id $pipelineId --output none
