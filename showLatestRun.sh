set -ev
if [ -z "$1" ]
  then
    echo "pass projectId"
    exit
fi
IFS='/'
read -a strarr <<<$(az webapp webjob triggered log --webjob-name GraphExcelEmailerWebJob --name $1-WebApp -g $1-RG --query [0].name -o tsv)
echo "https://${strarr[0]}.scm.azurewebsites.net/azurejobs/#/jobs/triggered/${strarr[1]}/runs/${strarr[2]}"
