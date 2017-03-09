---
services: active-directory
platforms: dotnet
author: dstrockis,jmprieur
---

# Authenticating to Azure AD in daemon apps with certificates - manual configuration steps
This page explains how to register the sample with your Azure Active Directory and how to configure the code to use your Azure AD tenant.
Alternatively the [Readme.md](./README.md) presents how to use a PowerShell script to  do the same thing automatically

### Step 1:  Register the sample with your Azure Active Directory tenant

There are two projects in this sample.  Each project needs to be separately registered in your Azure AD tenant.

#### Register the TodoListService web API

1. Sign in to the [Azure portal](https://portal.azure.com).
2. On the top bar, click on your account and under the **Directory** list, choose the Active Directory tenant where you wish to register your application.
2. Click on **More Services** in the left hand nav, and choose **Azure Active Directory**.
3. Click on **App registrations** and choose **Add**.
4. Enter a friendly name for the application, for example 'TodoListService' and select 'Web Application and/or Web API' as the Application Type. For the sign-on URL, enter the base URL for the sample, which is by default `https://localhost:44321`. For the App ID URI, enter `https://<your_tenant_name>/TodoListService`, replacing `<your_tenant_name>` with the name of your Azure AD tenant. Click on **Create** to create the application.
5. While still in the Azure portal, choose your application, click on **Settings** and choose **Properties**.
6. Find the Application ID value and copy it to the clipboard.

#### Register the TodoListDaemonWithCert app


1. Sign in to the [Azure portal](https://portal.azure.com).
2. On the top bar, click on your account and under the **Directory** list, choose the Active Directory tenant where you wish to register your application.
2. Click on **More Services** in the left hand nav, and choose **Azure Active Directory**.
3. Click on **App registrations** and choose **Add**.
4. Enter a friendly name for the application, for example 'TodoListDaemonWithCert' and select *'Web Application and/or Web API'* as the Application Type (even if here we have a console application). 
5. Since this application is a daemon and not a web application, it doesn't have a sign-in URL or app ID URI.  For these two fields, enter "http://TodoListDaemonWithCert". Click on **Create** to create the application.
6. While still in the Azure portal, choose your application, click on **Settings** and choose **Properties**.
7. Find the Application ID value and copy it to the clipboard.


#### Create a self-signed certificate

To complete this step you will use the `New-SelfSignedCertificate` Powershell command. You can find more information about the New-SelfSignedCertificat command [here](https://technet.microsoft.com/library/hh848633).

Open PowerShell and run New-SelfSignedCertificate with the following parameters to create a self-signed certificate in the user certificate store on your computer:

```
$cert=New-SelfSignedCertificate -Subject "CN=TodoListDaemonWithCert" -CertStoreLocation "Cert:\CurrentUser\My"  -KeyExportPolicy Exportable -KeySpec Signature 
```

If needed you can later export this certificate using the "Manage User Certificate" MMC snap-in accessible from the Windows Control Panel. You can also add other options to generate the certificate in a different
store such as the Computer or service store (See [How to: View Certificates with the MMC Snap-in](https://msdn.microsoft.com/en-us/library/ms788967)).

#### Add the certificate as a key for the TodoListDaemonWithCert application in Azure AD
##### Generate a textual file containing the certificate credentials in a form consumable by AzureAD
Copy and paste the following lines in the sam PowerShell window. They generate a text file in the current folder containing information that you can use to upload your certificate to Azure AD:

```
$bin = $cert.RawData
$base64Value = [System.Convert]::ToBase64String($bin)
$bin = $cert.GetCertHash()
$base64Thumbprint = [System.Convert]::ToBase64String($bin)
$keyid = [System.Guid]::NewGuid().ToString()
$jsonObj = @{customKeyIdentifier=$base64Thumbprint;keyId=$keyid;type="AsymmetricX509Cert";usage="Verify";value=$base64Value}
$keyCredentials=ConvertTo-Json @($jsonObj) | Out-File "keyCredentials.txt"

```
 The content of the generated "keyCredentials.txt" file has the following schema: 
```
[
    {
        "customKeyIdentifier": "$base64Thumbprint_from_above",
        "keyId": "$keyid_from_above",
        "type": "AsymmetricX509Cert",
        "usage": "Verify",
        "value":  "$base64Value_from_above"
    }
]
```

##### Associate the certificate credentials with the Azure AD Application
To associate the certificate credential with the TodoListDaemonWithCert app object in Azure AD, you will need to edit  the application manifest. In the Azure Management Portal app registration for the `TodoListDaemonWithCert`
click on **Manifest**. A blade opens enabling you to edit the manifest.
You need to replace the value of the `keyCredentials` property (that is `[]` if you don't have any certificate credentials yet), with the content of the keyCredential.txt file 

To do this replacement in the manifest, you have two options:
- Option 1: Edit the manifest in place by clicking **Edit**, replacing the keyCredentials value, and then clicking **Save**. 
Note that if you refresh the web page, the key is displayed with different properties than what you have input. In particular, you can now see the endDate, and stateDate, and the vlaue is shown as null. This is normal. 

- Option 2: **Download** the manifest to your computer, edit it with your favorite text editor, save a copy of it, and **Upload** this copy. You might want to choose this option if you want to keep track of the history of the manifest.

Note that the `keyCredentials` property is multi-valued, so you may upload multiple certificates for richer key management. In that case copy only the text between the curly brackets.


### Step 2:  Configure the sample to use your Azure AD tenant

#### Configure the TodoListDaemon project

1. Open `app.config'.
2. Find the app key `ida:Tenant` and replace the value with your AAD tenant name.
3. Find the app key `ida:ClientId` and replace the value with the Client ID for the TodoListDaemonWithCert app registration from the Azure portal.
4. Find the app key `ida:CertName` and replace the value with the subject name of the self-signed certificate you created, e.g. "CN=TodoListDaemonWithCert".
5. Find the app key `todo:TodoListResourceId` and replace the value with the  App ID URI of the TodoListService, for example `https://<your_tenant_name>/TodoListService`
6. Find the app key `todo:TodoListBaseAddress` and replace the value with the base address of the TodoListService project.

#### Configure the TodoListService project

1. Open the solution in Visual Studio 2013.
2. Open the `web.config` file.
3. Find the app key `ida:Tenant` and replace the value with your AAD tenant name.
4. Find the app key `ida:Audience` and replace the value with the App ID URI you registered earlier, for example `https://<your_tenant_name>/TodoListService`.
