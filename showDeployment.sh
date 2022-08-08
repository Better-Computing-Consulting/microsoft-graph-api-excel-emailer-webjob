#!/bin/bash

set -ev
if [ -z "$1" ]
  then
    echo "pass projectId"
    exit
fi
echo $'\e[0;92m'
az resource list --query "[?resourceGroup=='$1-RG'].{ name: name, flavor: kind, resourceType: type, resourceGroup: resourceGroup, CreatedTime: createdTime sku: sku.tier}" --output table

echo $'\e[0m'
echo $'\e[0;92m'
az webapp config appsettings list --name $1-WebApp --resource-group $1-RG --query "[?name=='KEY_VAULT_NAME'].{ name: name, value: value }" --output table

echo $'\e[0m'
localpubip=$(dig +short myip.opendns.com @resolver1.opendns.com)
az keyvault network-rule add --name $1-KV --ip-address $localpubip --output none
echo $'\e[0;92m'
az keyvault secret list --vault-name $1-KV -o table

echo $'\e[0m'
az keyvault network-rule remove --name $1-KV --ip-address $localpubip --output none
adAppId=$(az ad app list --display-name $1-ADApp --query [].appId)
reqAccess=$(az ad app show --id $adAppId --query requiredResourceAccess[].resourceAccess[].id)
graphId=$(az ad sp list --query "[?appDisplayName=='Microsoft Graph'].appId" --all)
echo $'\e[0;92m'
for i in $reqAccess
do
  az ad sp show --id $graphId --query "appRoles[?id=='$i'].value"
done

echo $'\e[0m'
echo $'\e[0;92m'
az devops project show --project $1 --query state -o table

echo $'\e[0m'
echo $'\e[0;92m'
az devops service-endpoint list --project $1 --query "[].{Name: name, Type: type}" --output table

echo $'\e[0m'
pipelines=$(az pipelines list --project $1 --query "[].id")
echo $'\e[0;92m'
for p in $pipelines
do
  printf "\nPipeline id $p info:\n"
  az pipelines show --id $p --project $1 --query "{name: name, project: project.name, repository: repository.name, yamlFilename: process.yamlFilename, createdDate: createdDate }" -o table
  printf "\nPipeline id $p variables:\n"
  az pipelines variable list --pipeline-id $p --project $1 -o table
  echo ""
done
echo $'\e[0;92m'
