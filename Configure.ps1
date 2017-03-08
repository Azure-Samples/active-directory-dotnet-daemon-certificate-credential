# This script creates the Azure AD applications needed for this sample and updates the configuration files
# for the visual Studio projects from the data in the Azure AD applications.
#
# Before running this script you need to install the AzureAD cmdlets as an administrator. 
# For this:
# 1) Run Powershell as an administrator
# 2) in the PowerShell window, type: Install-Module AzureAD
#
# Before you run this script
# 3) With the Azure portal (https://portal.azure.com), choose your active directory tenant, then go to the Properties of the tenant and copy
#    the DirectoryID. This is what we'll use in this script for the tenant ID
# 
# To configurate the applications
# 4) Run the following command:
#      $apps = ConfigureApplications -tenantId [place here the GUID representing the tenant ID]
#    You will be prompted by credentials, be sure to enter credentials for a user who can create applications
#    in the tenant
#
# To execute the samples
# 5) Build and execute the applications. This just works
#
# To cleanup
# 6) Optionnaly if you want to cleanup the applications in the Azure AD, run:
#      CleanUp $apps
#    The applications are un-registered
Import-Module AzureAD
$ErrorActionPreference = 'Stop'


# Replace the value of an appsettings of a given key in an XML App.Config file.
Function ReplaceSetting([string] $configFilePath, [string] $key, [string] $newValue)
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
This function creates the Azure AD applications for the sample in the provided Azure AD tenant and updates the
configuration files in the client and service project  of the visual studio solution (App.Config and Web.Config)
so that they are consistent with the Applications parameters
#>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$True, HelpMessage='Tenant ID (This is a GUID which represents the "Directory ID" of the AzureAD tenant into which you want to create the apps')]
        [string] $tenantId
    )

   process
   {
    # $tenantId is the Active Directory Tenant. This is a GUID which represents the "Directory ID" of the AzureAD tenant 
    # into which you want to create the apps. Look it up in the Azure portal in the "Properties" of the Azure AD. 

    # Login to Azure PowerShell (interactive: you'll need to sign-in with creds enabling your to create apps in the tenant)
    $creds = Connect-AzureAD -TenantId $tenantId
    if (!$tenantId)
    {
        $tenantId = $creds.Tenant.Id
    }
    $tenant = Get-AzureADTenantDetail
    $tenantName =  $tenant.VerifiedDomains[0].Name

    # Variables for the registration of the AAD application for the Web API Service
    $serviceAadAppName = "TodoListService"
    $serviceHomePage = "https://localhost:44321"
    $serviceAppIdURI = "https://$tenantName/$serviceAadAppName"

    # Variables for the registration of the AAD application for the Daemon app
    $daemonAadAppName = "TodoListDaemonWithCert"
    $daemonHomePage = "http://TodoListDaemonWithCert"
    $daemonAppIdURI = "http://TodoListDaemonWithCert"
    $certificateName = "CN=TodoListDaemonWithCert"

    # Create the Azure Active Directory Application and it's service principal
    Write-Host "Creating the service appplication ($serviceAadAppName)"
    $serviceApplication = New-AzureADApplication -DisplayName $serviceAadAppName `
                                                 -HomePage $serviceHomePage `
                                                 -IdentifierUris $serviceAppIdURI
    $serviceservicePrincipal = New-AzureADServicePrincipal -AppId $serviceApplication.AppId

    # Create the daemon application
    $daemonApplication = New-AzureADApplication -DisplayName $daemonAadAppName `
                                                -HomePage $daemonHomePage `
                                                -IdentifierUris $daemonAppIdURI
    $daemonservicePrincipal = New-AzureADServicePrincipal  -AppId $daemonApplication.AppId

    # Generate a certificate
    Write-Host "Creating the client appplication ($daemonAadAppName)"
    $certificate=New-SelfSignedCertificate -Subject $certificateName `
                                           -CertStoreLocation "Cert:\CurrentUser\My" `
                                           -KeyExportPolicy Exportable `
                                           -KeySpec Signature 
    $certKeyId = [Guid]::NewGuid()
    $certBase64Value = [System.Convert]::ToBase64String($certificate.GetRawCertData())
    $certBase64Thumbprint = [System.Convert]::ToBase64String($certificate.GetCertHash())
    
    # Add a Azure Key Credentials from the certificate for the daemon application
    $applicationKeyCredentials = New-AzureADApplicationKeyCredential -ObjectId $daemonApplication.ObjectId `
                                                                     -CustomKeyIdentifier $certificateName `
                                                                     -Type AsymmetricX509Cert `
                                                                     -Usage Verify `
                                                                     -Value $certBase64Value `
                                                                     -StartDate $certificate.NotBefore `
                                                                     -EndDate $certificate.NotAfter

    # Update the config file in the daemon application
    $configFile = $pwd.Path + "\TodoListDaemonWithCert\App.Config"
    Write-Host "Updating the configuration file for the client ($configFile)"
    UpdateClientConfigFile -configFilePath $configFile `
       -tenantId $tenantId `
       -clientId $daemonApplication.AppId `
       -appUri $serviceAppIdURI `
       -baseAddress $serviceHomePage `
       -certificateName $certificateName

    # Update the config file in the service application
    $configFile = $pwd.Path + "\TodoListService\Web.Config"
    Write-Host "Updating the client sample ($configFile)"
    UpdateServiceConfigFile -configFilePath $configFile `
                            -tenantId $tenantId `
                            -audience $serviceAppIdURI

    # prepare the clean-up
    $applicationsInformation = @{serviceApp=$serviceApplication; serviceAppServicePrincipal=$serviceservicePrincipal; clientApp=$daemonApplication; clientAppServicePrincipal=$daemonservicePrincipal; certificateThumbPrint=$certBase64Thumbprint}
    return $applicationsInformation
   }
}


# Remove the AAD applications and service principals registered for the sample.
Function CleanUp($applicationsInformation)
{
    Remove-AzureADServicePrincipal -ObjectId $applicationsInformation.serviceAppServicePrincipal.ObjectId
    Remove-AzureADApplication -ObjectId $applicationsInformation.serviceApp.ObjectId
    Remove-AzureADServicePrincipal -ObjectId $applicationsInformation.clientAppServicePrincipal.ObjectId
    Remove-AzureADApplication -ObjectId $applicationsInformation.clientApp.ObjectId
}


# Run interactively (will ask you for the tenant ID)
$apps = ConfigureApplications 

# you can also provide the tenant ID
# $tenantId = "ID of your AAD directory"
# $apps = ConfigureApplications -tenantId $tenantId 


# When you have built your Visual Studio solution and ran the code, if you want to clean up the Azure AD applications, just 
# run the following command in the same PowerShell window as you ran ConfigureApplications
# CleanUp($apps)