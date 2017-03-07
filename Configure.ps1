# This script creates the Azure AD applications needed for this sample and update the configuration files
# for the visual Studio projects from the data in the Azure AD applications.
#
# Before running this script you need to install the Azure RM cmdlets as an administrator. 
# For this:
# 1) Run Powershell as an administrator
# 2) in the PowerShell window, type: Install-Module AzureRM.Resources
#
# To prepare this script
# 3) With the Azure portal (https://portal.azure.com), choose your active directory tenant, then go to the Properties of the tenant and copy
#    the DirectoryID. This is what we'll use in this script for the tenant ID
# 
# To configurate the applications
# 4) Run the following command:
#      $apps = ConfigureApplications -tenantId [place here the GUID representing the tenant ID]
#
# To execute the samples
# 5) Build and execute the applications. This just works
#
# To cleanup
# 6) Optionnaly if you want to cleanup the applications in the Azure AD, run:
#      cleanup($apps)
#    The applications are un-registered

$ErrorActionPreference = 'Stop'


# Replace the value of an appsettings of a given key in an XML App.Config file.
Function ReplaceSetting([string] $configFilePath, [string] $key, [string]$newValue)
{
    [xml] $content = Get-Content $configFilePath
    $appSettings = $content.configuration.appSettings; 
    $keyValuePair = $appSettings.SelectSingleNode("descendant::add[@key='$key']")
    if ($keyValuePair)
    {
        $keyValuePair.value = $newValue;
    }
    else
    {
        Throw "Key '$key' not found in file '$configFilePath'"
    }
   $content.save($configFilePath)
}

# Updates the config file for a client application
Function UpdateClientConfigFile([string] $configFilePath, [string] $tenantId, [string] $clientId, [string] $appUri, [string] $baseAddress, [string] $certificateName)
{
    ReplaceSetting -configFilePath $configFilePath -key "ida:Tenant" -newValue $tenantId
    ReplaceSetting -configFilePath $configFilePath -key "ida:ClientId" -newValue $clientId
    ReplaceSetting -configFilePath $configFilePath -key "todo:TodoListResourceId" -newValue $appUri
    ReplaceSetting -configFilePath $configFilePath -key "todo:TodoListBaseAddress" -newValue $baseAddress
    if ($certificateName)
    {
        ReplaceSetting -configFilePath $configFilePath -key "ida:CertName" -newValue $certificateName
    }
}


# Updates the config file for a client application
Function UpdateServiceConfigFile([string] $configFilePath, [string] $tenantId, [string] $audience)
{
    ReplaceSetting -configFilePath $configFilePath -key "ida:Tenant" -newValue $tenantId
    ReplaceSetting -configFilePath $configFilePath -key "ida:Audience" -newValue $audience
}


Function ConfigureApplications
{
<#
.Description
This function creates the Azure AD applications for the sample in the provided Azure AD tenant and updates the configuration files in the client and service project 
of the visual studio solution (App.Config and Web.Config) so that they are consistent with the Applications parameters
#>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$True, HelpMessage='Tenant ID (This is a GUID which represents the "Directory ID" of the AzureAD tenant into which you want to create the apps')]
        [string] $tenantId
    )

   process
   {
    # Active Directory Tenant. This is a GUID which represents the "Directory ID" of the AzureAD tenant into which you want to create the apps.
    # Look it up in the Azure portal in the "Properties" of the Azure AD. 
    # IN THE FUTURE: We'd like to be able to get the tenant name from the tenant ID (or pass the tenant name as an argument)
    $tenantName = $tenantId

    # Variables for the registration of the AAD application for the Web API Service
    $serviceAadAppName = "TodoListService"
    $serviceHomePage = "https://localhost:44321"
    $serviceAppIdIRI = "https://"+$tenantName+"/"+$serviceAadAppName

    # Variables for the registration of the AAD application for the Daemon app
    $daemonAadAppName = "TodoListDaemonWithCert"
    $daemonHomePage = "http://TodoListDaemonWithCert"
    $daemonAppIdIRI = "http://TodoListDaemonWithCert"
    $certificateName = "CN=TodoListDaemonWithCert"

    # Import required modules
    Import-Module AzureRM.Resources

    # Login to Azure PowerShell (interactive: you'll need to sign-in with creds enabling your to create apps in the tenant)
    $creds = Login-AzureRmAccount -TenantId $tenantId

    # Create the Azure Active Directory Application and it's service principal
    # Note that if, at this point, you get an error: "New-AzureRmADApplication : Your Azure credentials have not been set up or have expired, please run Login-AzureRMAccount to set up your Azure credentials"
    # then you will need to run Clear-AzureProfile (you might have an expired token)
    Write-Host "Creating the service appplication ($serviceAadAppName)"
    $serviceApplication = New-AzureRmADApplication -DisplayName $serviceAadAppName -HomePage $serviceHomePage -IdentifierUris $serviceAppIdIRI
    $serviceservicePrincipal = New-AzureRmADServicePrincipal -ApplicationId $serviceApplication.ApplicationId

    # Create the daemon application
    # ------------------------------
    # Generate a certificate
    Write-Host "Creating the client appplication ($daemonAadAppName)"
    $certificate=New-SelfSignedCertificate -Subject $certificateName -CertStoreLocation "Cert:\CurrentUser\My"  -KeyExportPolicy Exportable -KeySpec Signature 
    $certKeyId = [Guid]::NewGuid()

    # Create Azure Key Credentials from the certificate
    $keyCredential = New-Object Microsoft.Azure.Commands.Resources.Models.ActiveDirectory.PSADKeyCredential
    $keyCredential.KeyId = $certKeyId
    $keyCredential.CertValue = [System.Convert]::ToBase64String($certificate.GetRawCertData())
    $keyCredential.StartDate = $certificate.NotBefore
    $keyCredential.EndDate= $certificate.NotAfter

    # And create the daemon application
    $daemonApplication = New-AzureRmADApplication -DisplayName $daemonAadAppName -HomePage $daemonHomePage -IdentifierUris $daemonAppIdIRI -KeyCredentials $keyCredential
    $daemonservicePrincipal = New-AzureRmADServicePrincipal -ApplicationId $daemonApplication.ApplicationId

    # Update the config file in the daemon application
    $configFile = $pwd.Path + "\TodoListDaemonWithCert\App.Config"
    Write-Host "Updating the configuration file for the client ($configFile)"
    UpdateClientConfigFile -configFilePath $configFile -tenantId $tenantId -clientId $daemonApplication.ApplicationId -appUri $serviceAppIdIRI -baseAddress $serviceHomePage -certificateName $certificateName

    # Update tehe config file in the service application
    $configFile = $pwd.Path + "\TodoListService\Web.Config"
    Write-Host "Updating the client sample ($configFile)"
    UpdateServiceConfigFile -configFilePath $configFile -tenantId $tenantId -audience $serviceAppIdIRI

    # prepare the clean-up
    $applicationsInformation = @{serviceApp=$serviceApplication; serviceAppServicePrincipal=$serviceservicePrincipal; clientApp=$daemonApplication; clientAppServicePrincipal=$daemonservicePrincipal;}
    return $applicationsInformation
   }
}


#Clean-Ups the applications and serice principal in Azure AD.
Function CleanUp($applicationsInformation)
{
    remove-azurermadserviceprincipal -objectid $applicationsInformation.serviceAppServicePrincipal.id -force
    remove-azurermadapplication -objectid $applicationsInformation.serviceApp.objectid -force
    remove-azurermadserviceprincipal -objectid $applicationsInformation.clientAppServicePrincipal.id -force
    remove-azurermadapplication -objectid $applicationsInformation.clientApp.objectid -force
}

$apps = ConfigureApplications
# Cleanup($apps)