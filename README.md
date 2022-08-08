# GraphExcelEmailer

This project is composed of a .net Console application that sends new emails and reply emails based on the contents of several tables contained in an Excel file stored in OneDrive. The application uses the Microsoft Graph SDK to send emails on behalf of a user’s account and to access the excel document stored in the user’s account.  The application uses app-only, OAuth2 authentication to access the Graph API. The application obtains the secretes required to access the Graph API from an Azure KeyVault. Further, the console application is deployed as an Azure WebApp WebJob, and the Azure KeyVault restricts access to its secrets to only the public IP and Managed Identity of the WebApp.

The goal of the console app is to send two, and only two emails to people listed on the table located on Sheet1 of the spreadsheet. Once the application sends one email to people listed on the first table, the program moves the entry to a table located on Sheet2 of the excel document. In this second table the program adds the InternetMessageId and the datetime sent of the original email.  So that when the program runs it will check the entries of the second table, and if any message is older than six days, it will locate this message by its InternetMessageId in the Sent Items folder of the user configured in the appsettings.jason file of the application.  After the message is located, the program sends a ReplyAll message to the original recipient following up on the original message. Once a Reply message is sent to a person, the entry is moved to a table on the Sheet3 of the excel spreadsheet, adding the date of the second email. The program will not send an email to people listed on the table on Sheet3 of the document. The program will not send messages to emails listed in a fourth table, in Sheet4 of the excel document. 

The project also includes a deployResourcesProject.sh shell script that deploys and configures all the Azure Resources, and the Azure DevOps project that builds, deploys, and executes the WebJob. Lastly, the project includes a showDeployment.sh script that displays all the resources deployed and their configurations.

To try this project first run the deployResourcesProject.sh on shell prompt with the AZ CLI installed with an account with enough rights to create AD accounts and deploy resources.

The deployResourcesProject.sh will:

1)	Setup a project name variable based on root project id plus a random number.  The names of all other resources will be based on this project id.
2)	Create the Azure Resource group.
3)	Create an App Service Plan with an SKU of Free (all the resources required for the project can be deployed for free)
4)	Deploy a WebApp and store the Managed ID of the WebApp.
5)	Get the public IPs of the WebApp.
6)	Deploy an Azure Key Vault with a default access policy of Deny and grant access to the WebApp public IPs.
7)	Add an Access Policy to the Key Vault that allows the WebApp’s Managed Identity access to the secrets.
8)	Add the Key Vault name as an Environment variable of the WebApp. This allows the console application to learn the address of the KeyVault
9)	Register the AD App that the console will use to authenticate to the Graph API.
10)	Setup a password for the AD App.
11)	Add the required Graph permissions to the AD App
12)	Grant and Admin-consent the permissions.
13)	Store the AD App ID and password as secrets on the Key Vault.
14)	Create a service principal for Azure DevOps project with enough access to deploy the WebJob into the WebApp and scoped only to its Resource Group.
15)	Create the Azure DevOps project
16)	Add an Azure RM service endpoint to the project using the service principal created in step 14.
17)	Add a GitHub service endpoint the project.
18)	Create a pipeline for building and deploying the console application into the WebApp using the azure-pipelines.yml file included in this repository.
19)	Add a variable the pipeline with the name of the WebApp to deploy the application.
20)	Create a pipeline for running the WebApp based on a corn schedule. (The Free SKU of the App Service Plain does not allow for WebJobs to be triggered by a cron schedule, so we are using a cron job in an Azure DevOps pipeline to run the WebJob instead.
21)	Add two variables to the cron pipeline: the name of the WebApp and its resource group, both of which are required by the run webjob command.


One the resources are deployed we are ready to build and deploy the console app either by committing a change to the GitHub repository or by clicking Run on the pipeline.

Enjoy 
