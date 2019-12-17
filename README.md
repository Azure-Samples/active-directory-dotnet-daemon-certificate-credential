---
services: active-directory
platforms: dotnet
author: jmprieur
level: 200
client: Desktop
service: ASP.NET Web API
endpoint: AAD V1
---
# Authenticating to Azure AD in daemon apps with certificates

![Build badge](https://identitydivision.visualstudio.com/_apis/public/build/definitions/a7934fdd-dcde-4492-a406-7fad6ac00e17/30/badge)

## About this sample

 The application uses the Active Directory Authentication Library (ADAL) to get a token from Azure AD using the [OAuth 2.0 client credential](https://docs.microsoft.com/azure/active-directory/develop/active-directory-protocols-oauth-service-to-service) flow, where the client credential is a certificate.

> There's a newer version of this sample! Check it out: https://github.com/azure-samples/ms-identity-dotnetcore-daemon-console
>
> This newer sample takes advantage of the Microsoft identity platform(formerly Azure AD v2.0).
>
> While still in public preview, every component is supported in production environments

### Overview

In this sample, a Windows console application (TodoListDaemonWithCert) calls a web API (TodoListService) using its app identity. This scenario is useful for situations where headless or unattended job or a windows service needs to run with an application identity, instead of a user's identity.

This sample is similar to [Daemon-DotNet](https://github.com/Azure-Samples/active-directory-dotnet-daemon), except instead of the daemon using a password as a credential to authenticate with Azure AD, it uses a certificate.

## Topology

![Overview](./ReadmeFiles/Topology.png)

### Scenario

Once the service started, when you start the `TodoListDaemon` desktop application, it repeatedly:

- adds items to the todo list maintained by the service,
- lists the existing todo list items.

No user interaction is involved in this sample.

![Overview](./ReadmeFiles/TodoListDaemon.png)

## How to run this sample

To run this sample, you'll need:

- [Visual Studio 2017](https://aka.ms/vsdownload)
- An Internet connection
- An Azure Active Directory (Azure AD) tenant. For more information on how to get an Azure AD tenant, see [How to get an Azure AD tenant](https://azure.microsoft.com/en-us/documentation/articles/active-directory-howto-tenant/)
- A user account that is an **admin of your Azure AD tenant**. This sample will not work with a Microsoft account (formerly Windows Live account). Therefore, if you signed in to the [Azure portal](https://portal.azure.com) with a Microsoft account and have never created a user account in your directory before, you need to do that now.

### Step 1:  Clone or download this repository

You can clone this repository from Visual Studio. Alternatively, from your shell or command line, use:

```Shell
git clone https://github.com/Azure-Samples/active-directory-dotnet-daemon-certificate-credential.git
```

> Given that the name of the sample is pretty long, and so are the name of the referenced NuGet packages, you might want to clone it in a folder close to the root of your hard drive, to avoid file size limitations on Windows.

### Step 2:  Register the sample with your Azure Active Directory tenant

There are two projects in this sample. Each needs to be separately registered in your Azure AD tenant. To register these projects, you can:

- either follow the steps [Step 2: Register the sample with your Azure Active Directory tenant](#step-2-register-the-sample-with-your-azure-active-directory-tenant) and [Step 3:  Configure the sample to use your Azure AD tenant](#choose-the-azure-ad-tenant-where-you-want-to-create-your-applications)
- or use PowerShell scripts that:
  - **automatically** creates the Azure AD applications and related objects (passwords, permissions, dependencies) for you
  - modify the Visual Studio projects' configuration files.

If you want to use this automation:
1. On Windows run PowerShell and navigate to the root of the cloned directory
1. In PowerShell run:
   ```PowerShell
   Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope Process -Force
   ```
1. Run the script to create your Azure AD application and configure the code of the sample application accordingly. 
   ```PowerShell
   .\AppCreationScripts\Configure.ps1
   ```
   > Other ways of running the scripts are described in [App Creation Scripts](./AppCreationScripts/AppCreationScripts.md)

> For Windows Server 2012, creating a certificate with PowerShell is slightly different: See issue [#37](https://github.com/Azure-Samples/active-directory-dotnet-daemon-certificate-credential/issues/37)

#### First step: choose the Azure AD tenant where you want to create your applications

As a first step you'll need to:

1. Sign in to the [Azure portal](https://portal.azure.com) using either a work or school account or a personal Microsoft account.
1. If your account is present in more than one Azure AD tenant, select your profile at the top right corner in the menu on top of the page, and then **switch directory**.
   Change your portal session to the desired Azure AD tenant.

#### Register the service app (TodoListService-Cert)

1. Navigate to the Microsoft identity platform for developers [App registrations](https://go.microsoft.com/fwlink/?linkid=2083908) page.
1. Select **New registration**.
1. When the **Register an application page** appears, enter your application's registration information:
   - In the **Name** section, enter a meaningful application name that will be displayed to users of the app, for example `TodoListService-Cert`.
   - Leave **Supported account types** on the default setting of **Accounts in this organizational directory only**.
1. Select **Register** to create the application.
1. On the app **Overview** page, find the **Application (client) ID** value and record it for later. You'll need it to configure the Visual Studio configuration file for this project.

1. Select the **Expose an API** section, and:
   - Select **Add a scope**
   - accept the proposed Application ID URI (api://{clientId}) by selecting **Save and Continue**
   - Enter the following parameters
     - for **Scope name** use `access_as_application`
     - Keep **Admins and users** for **Who can consent**
     - in **Admin consent display name** type `Access TodoListService-Cert as an application`
     - in **Admin consent description** type `Accesses the TodoListService-Cert Web API an application`
     - in **User consent display name** type `Access TodoListService-Cert as an application`
     - in **User consent description** type `Accesses the TodoListService-Cert Web API as an application`
     - Keep **State** as **Enabled**
     - Select **Add scope**

#### Secure your Web API by defining Application Roles (permission)

If you don't do anything more, Azure AD will provide a token for any daemon application (using the client credential flow) requesting an access token for your Web API (for its App ID URI)

In this step we are going to ensure that Azure AD only provides a token to the applications to which the Tenant admin grants consent. We are going to limit the access to our TodoList client by defining authorizations

##### Add an app role to the manifest

1. While still in the blade for your  application, click **Manifest**.
1. Edit the manifest by locating the `appRoles` setting and adding an application roles. The role definition is provided in the JSON block below.  Leave the `allowedMemberTypes` to "Application" only.
1. Save the manifest.

The content of `appRoles` should be the following (the `id` can be any unique GUID)

```JSon
"appRoles": [
	{
	"allowedMemberTypes": [ "Application" ],
	"description": "Accesses the TodoListService-Cert as an application.",
	"displayName": "access_as_application",
	"id": "ccf784a6-fd0c-45f2-9c08-2f9d162a0628",
	"isEnabled": true,
	"lang": null,
	"origin": "Application",
	"value": "access_as_application"
	}
],
```

##### Ensure that tokens Azure AD issues tokens for your Web API only to allowed clients

The Web API tests for the app role (that's the developer way of doing it). But you can even ask Azure Active Directory to issue a token for your Web API only to applications which were approved by the tenant admin. For this:

1. On the app **Overview** page for your app registration, select the hyperlink with the name of your application in **Managed application in local directory** (note this field title can be truncated for instance Managed application in ...)

   > When you select this link you will navigate to the **Enterprise Application Overview** page associated with the service principal for your application in the tenant where you created it. You can navigate back to the app registration page by using the back button of your browser.

1. Select the **Properties** page in the **Manage** section of the Enterprise application pages
1. If you want AAD to enforce access to your Web API from only certain clients, set **User assignment required?** to **Yes**.

   > **Important security tip**
   >
   > By setting **User assignment required?** to **Yes**, AAD will check the app role assignments of the clients when they request an access token for the Web API (see app permissions below). If the client was not be assigned to any AppRoles, AAD would just return `invalid_client: AADSTS501051: Application xxxx is not assigned to a role for the xxxx`
   >
   > If you keep **User assignment required?** to **No**, <span style='background-color:yellow; display:inline'>Azure AD  won’t check the app role assignments  when a client requests an access token to your Web API</span>. Therefore, any daemon client (that is any client using client credentials flow) would still be able to obtain the access token for the  Web API just by specifying its audience. Any application, would be able to access the API without having to request permissions for it. Now this is not then end of it, as your Web API can always, as is done in this sample, verify that the application has the right role (which was authorized by the tenant admin), by validating that the access token has a roles claim, and 

1. Select **Save**

#### Register the client app (TodoListDaemon-Cert)

1. Navigate to the Microsoft identity platform for developers [App registrations](https://go.microsoft.com/fwlink/?linkid=2083908) page.
1. Select **New registration**.
1. When the **Register an application page** appears, enter your application's registration information:
   - In the **Name** section, enter a meaningful application name that will be displayed to users of the app, for example `TodoListDaemon-Cert`.
   - Leave **Supported account types** on the default setting of **Accounts in this organizational directory only**.
   - In the Redirect URI (optional) section, select **Web** in the combo-box.
      > Even if this is a desktop application, this is a confidential client application hence the *Application Type* being 'Web', which might seem counter intuitive.
   - For the Redirect URI*, enter `https://<your_tenant_name>/TodoListDaemon-Cert`, replacing `<your_tenant_name>` with the name of your Azure AD tenant.
1. Select **Register** to create the application.
1. On the app **Overview** page, find the **Application (client) ID** value and record it for later. You'll need it to configure the Visual Studio configuration file for this project.

#### Create a self-signed certificate

To complete this step, you will use the `New-SelfSignedCertificate` Powershell command. You can find more information about the New-SelfSignedCertificate command [here](https://docs.microsoft.com/en-us/powershell/module/pkiclient/new-selfsignedcertificate).

1. Open PowerShell and run New-SelfSignedCertificate with the following parameters to create a self-signed certificate in the user certificate store on your computer:

```PowerShell
$cert=New-SelfSignedCertificate -Subject "CN=TodoListDaemonWithCert" -CertStoreLocation "Cert:\CurrentUser\My"  -KeyExportPolicy Exportable -KeySpec Signature
```

> It has been reported that certificates created this way may work in Azure Active Directory after few hours delay.

1. If needed, you can later export  this certificate using the "Manage User Certificate" MMC snap-in accessible from the Windows Control Panel. You can also add other options to generate the certificate in a different
store such as the Computer or service store (See [How to: View Certificates with the MMC Snap-in](https://docs.microsoft.com/en-us/dotnet/framework/wcf/feature-details/how-to-view-certificates-with-the-mmc-snap-in)).

#### Add the certificate for the TodoListDaemon-Cert application in Azure AD

##### Generate a textual file containing the certificate credentials in a form consumable by AzureAD

Copy and paste the following lines in the same PowerShell window. They generate a text file in the current folder containing information that you can use to upload your certificate to Azure AD:

```PowerShell
$bin = $cert.RawData
$base64Value = [System.Convert]::ToBase64String($bin)
$bin = $cert.GetCertHash()
$base64Thumbprint = [System.Convert]::ToBase64String($bin)
$keyid = [System.Guid]::NewGuid().ToString()
$jsonObj = @{customKeyIdentifier=$base64Thumbprint;keyId=$keyid;type="AsymmetricX509Cert";usage="Verify";value=$base64Value}
$keyCredentials=ConvertTo-Json @($jsonObj) | Out-File "keyCredentials.txt"
```

The content of the generated "keyCredentials.txt" file has the following schema:

```Json
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

To associate the certificate credential with the `TodoListDaemon-Cert` app object in Azure AD, you'll need to edit the application manifest.
In the Azure portal app registration page for the `TodoListDaemon-Cert`, click on **Manifest**. An editor window opens enabling you to edit the manifest.
You need to replace the value of the `keyCredentials` property (that is `[]` if you don't have any certificate credentials yet), with the content of the keyCredential.txt file.

To do this replacement in the manifest, you have two options:

- Option 1: Edit the manifest in place by clicking **Edit**, replacing the `keyCredentials` value, and then clicking **Save**.
  > Note that if you refresh the web page, the key is displayed with different properties than what you have input. In particular, you can now see the endDate, and stateDate, and the value is shown as null. This is normal.

- Option 2: **Download** the manifest to your computer, edit it with your favorite text editor, save a copy of it, and **Upload** this copy. You might want to choose this option if you want to keep track of the history of the manifest.

Note that the `keyCredentials` property is multi-valued, so you may upload multiple certificates for richer key management. In that case copy only the text between the curly brackets.

1. Select the **API permissions** section
   - Click the **Add a permission** button and then,
   - Ensure that the **My APIs** tab is selected
   - In the list of APIs, select the API `TodoListService-Cert`.
   - In the **Delegated permissions** section, ensure that the right permissions are checked: **Access 'TodoListService-Cert'**. Use the search box if necessary.
   - Select the **Add permissions** button

1. At this stage permissions are assigned correctly but the client app does not allow interaction. 
   Therefore no consent can be presented via a UI and accepted to use the service app. 
   Click the **Grant/revoke admin consent for {tenant}** button, and then select **Yes** when you are asked if you want to grant consent for the
   requested permissions for all account in the tenant.
   You need to be an Azure AD tenant admin to do this.

### Step 3:  Configure the sample to use your Azure AD tenant

In the steps below, "ClientID" is the same as "Application ID" or "AppId".

Open the solution in Visual Studio to configure the projects

#### Configure the service project

1. Open the `TodoListService\Web.Config` file
1. Find the app key `ida:Tenant` and replace the existing value with your Azure AD tenant name.
1. Find the app key `ida:Audience` and replace the existing value with the App ID URI you registered earlier in the form of `api://{clientId}`.

#### Configure the client project

1. Open the `TodoListDaemonWithCert\App.Config` file
1. Find the app key `ida:Tenant` and replace the existing value with your Azure AD tenant name.
1. Find the app key `ida:ClientId` and replace the existing value with the application ID (clientId) of the `TodoListDaemon` application copied from the Azure portal.
1. Find the app key `ida:CertName` and replace the existing value with Certificate.
1. Find the app key `todo:TodoListResourceId` and replace the existing value with the App ID URI you registered earlier in the form of `api://{clientId}`.
1. Find the app key `todo:TodoListBaseAddress` and replace the existing value with the base address of the TodoListService project (by default `https://localhost:44321/`).

### Step 4: Run the sample

Clean the solution, rebuild the solution, and run it.  You might want to go into the solution properties and set both projects as startup projects, with the service project starting first. To do this, you can for instance:

 1. Right click on the solution in the solution explorer and choose **Set Startup projects** from the context menu.
 2. choose **Multiple startup projects**
    - TodoListDaemonWithCert: **Start**
    - TodoListService: Start **Start without debugging**
 3. In the Visual Studio tool bar, press the **start** button: a web window appears running the service and a console application runs the daemon application under debugger. you can set breakpoints to understand the call to ADAL.NET.

The daemon will add items to the To Do list and then read them back.

## How to deploy this sample to Azure

This project has one WebApp / Web API projects. To deploy them to Azure Web Sites, you'll need, for each one, to:

- create an Azure Web Site
- publish the Web App / Web APIs to the web site, and
- update its client(s) to call the web site instead of IIS Express.

### Create and publish the `TodoListService-Cert` to an Azure Web Site

1. Sign in to the [Azure portal](https://portal.azure.com).
1. Click `Create a resource` in the top left-hand corner, select **Web** --> **Web App**, and give your web site a name, for example, `TodoListService-Cert-contoso.azurewebsites.net`.
1. Thereafter select the `Subscription`, `Resource Group`, `App service plan and Location`. `OS` will be **Windows** and `Publish` will be **Code**.
1. Click `Create` and wait for the App Service to be created.
1. Once you get the `Deployment succeeded` notification, then click on `Go to resource` to navigate to the newly created App service.
1. Once the web site is created, locate it it in the **Dashboard** and click it to open **App Services** **Overview** screen.

1. From the **Overview** tab of the App Service, download the publish profile by clicking the **Get publish profile** link and save it.  Other deployment mechanisms, such as from source control, can also be used.
1. Switch to Visual Studio and go to the TodoListService-Cert project.  Right click on the project in the Solution Explorer and select **Publish**.  Click **Import Profile** on the bottom bar, and import the publish profile that you downloaded earlier.
1. Click on **Configure** and in the `Connection tab`, update the Destination URL so that it is a `https` in the home page url, for example [https://TodoListService-Cert-contoso.azurewebsites.net](https://TodoListService-Cert-contoso.azurewebsites.net). Click **Next**.
1. On the Settings tab, make sure `Enable Organizational Authentication` is NOT selected.  Click **Save**. Click on **Publish** on the main screen.
1. Visual Studio will publish the project and automatically open a browser to the URL of the project.  If you see the default web page of the project, the publication was successful.

### Update the Active Directory tenant application registration for `TodoListService-Cert`

1. Navigate back to to the [Azure portal](https://portal.azure.com).
In the left-hand navigation pane, select the **Azure Active Directory** service, and then select **App registrations (Preview)**.
1. In the resultant screen, select the `TodoListService-Cert` application.
1. From the *Branding* menu, update the **Home page URL**, to the address of your service, for example [https://TodoListService-Cert-contoso.azurewebsites.net](https://TodoListService-Cert-contoso.azurewebsites.net). Save the configuration.
1. Add the same URL in the list of values of the *Authentication -> Redirect URIs* menu. If you have multiple redirect urls, make sure that there a new entry using the App service's Uri for each redirect url.

### Update the `TodoListDaemon-Cert` to call the `TodoListService-Cert` Running in Azure Web Sites

1. In Visual Studio, go to the `TodoListDaemon-Cert` project.
2. Open `TodoListDaemonWithCert\App.Config`.  Only one change is needed - update the `todo:TodoListBaseAddress` key value to be the address of the website you published,
   for example, [https://TodoListService-Cert-contoso.azurewebsites.net](https://TodoListService-Cert-contoso.azurewebsites.net).
3. Run the client! If you are trying multiple different client types (for example, .Net, Windows Store, Android, iOS) you can have them all call this one published web API.

> NOTE: Remember, the To Do list is stored in memory in this TodoListService sample. Azure Web Sites will spin down your web site if it is inactive, and your To Do list will get emptied.
Also, if you increase the instance count of the web site, requests will be distributed among the instances. ToDo list will, therefore, not be the same on each instance.

## About the Code

### Client side: the daemon app

The code acquiring a token is entirely located in the `TodoListDaemonWithCert\Program.cs` file.
The `AuthenticationContext` is created (line 76)

```CSharp
authContext = new AuthenticationContext(authority);
```

Then a `ClientAssertionCertificate` is instantiated line 87, from the `TodoListDaemon` application's Client ID and a certificate (`cert`) which was found from the certificate store (see lines 72-89).

```CSharp
certCred = new ClientAssertionCertificate(clientId, cert);
```

This instance of `ClientAssertionCertificate` is used in the `GetAccessToken()` method is as an argument to `AcquireTokenAsync` to get a token for the Web API (line 147)
`GetAccessToken()` is itself called from `PostTodo()` and `GetTodo()` methods.

```CSharp
result = await authContext.AcquireTokenAsync(todoListResourceId, certCred);
```

This token is then used as a bearer token to call the Web API (line 186 and 216)

```CSharp
httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken)
```

### Service side: how we protected the API

On the service side, the code directing ASP.NET to validate the access token is in `App_Start\Startup.Auth.cs`. It only validates the audience of the application (the App ID URI)

```CSharp
 public partial class Startup
 {
  // For more information on configuring authentication, please visit http://go.microsoft.com/fwlink/?LinkId=301864
  public void ConfigureAuth(IAppBuilder app)
  {
   app.UseWindowsAzureActiveDirectoryBearerAuthentication(
      new WindowsAzureActiveDirectoryBearerAuthenticationOptions
      {
       Tenant = ConfigurationManager.AppSettings["ida:Tenant"],
       TokenValidationParameters = new TokenValidationParameters
       {
        ValidAudience = ConfigurationManager.AppSettings["ida:Audience"]
       }
      });
   }
}
```

However, the controllers also validate that the client has a `roles` claim of value `access_as_application`. It returns an Unauthorized error otherwise.

```CSharp
 public IEnumerable<TodoItem> Get()
 {
  //
  // The roles claim tells what permissions the client application has in the service.
  // In this case we look for a roles value of access_as_application
  //
  Claim scopeClaim = ClaimsPrincipal.Current.FindFirst("roles");
  if (scopeClaim == null || (scopeClaim.Value != "access_as_application"))
  {
   throw new HttpResponseException(new HttpResponseMessage { StatusCode = HttpStatusCode.Unauthorized,
      ReasonPhrase = "The 'roles' claim does not contain 'access_as_application'or was not found" });
  }
  ...
 }
```

## How to recreate this sample

First, in Visual Studio 2017 (or above) create an empty solution to host the  projects.  Then, follow these steps to create each project.

### Creating the TodoListService Project

1. In the solution, create a new ASP.Net MVC web API project called TodoListService and while creating the project, click the Change Authentication button, select Organizational Accounts, Cloud - Single Organization, enter the name of your Azure AD tenant, and set the Access Level to Single Sign On.  You will be prompted to sign in to your Azure AD tenant.  NOTE:  You must sign in with a user that is in the tenant; you cannot, during this step, sign in with a Microsoft account.
2. In the `Models` folder, add a new class called `TodoItem.cs`.  Copy the implementation of TodoItem from this sample into the class.
3. Add a new, empty, Web API 2 controller called `TodoListController`.
4. Copy the implementation of the TodoListController from this sample into the controller.  Don't forget to add the `[Authorize]` attribute to the class.
5. In `TodoListController` resolving missing references by adding `using` statements for `System.Collections.Concurrent`, `TodoListService.Models`, `System.Security.Claims`.

### Creating the TodoListDaemon Project

1. In the solution, create a new Windows --> Console Application called TodoListDaemon.
2. Add the (stable) Active Directory Authentication Library (ADAL) NuGet, Microsoft.IdentityModel.Clients.ActiveDirectory, version 1.0.3 (or higher) to the project.
3. Add  assembly references to `System.Net.Http`, `System.Web.Extensions`, and `System.Configuration`.
4. Add a new class to the project called `TodoItem.cs`.  Copy the code from the sample project file of the same name into this class, completely replacing the code in the new file.
5. Copy the code from `Program.cs` in the sample project into the file of the same name in the new project, completely replacing the code in the new file.
6. In `app.config` create keys for `ida:AADInstance`, `ida:Tenant`, `ida:ClientId`, `ida:CertName`, `todo:TodoListResourceId`, and `todo:TodoListBaseAddress` and set them accordingly.  For the global Azure cloud, the value of `ida:AADInstance` is `https://login.microsoftonline.com/{0}`.

Finally, in the properties of the solution itself, set both projects as startup projects.

## Community Help and Support

Use [Stack Overflow](http://stackoverflow.com/questions/tagged/adal) to get support from the community.
Ask your questions on Stack Overflow first and browse existing issues to see if someone has asked your question before.
Make sure that your questions or comments are tagged with [`adal` `dotnet`].

If you find a bug in the sample, please raise the issue on [GitHub Issues](../../issues).

To provide a recommendation, visit the following [User Voice page](https://feedback.azure.com/forums/169401-azure-active-directory).

## Contributing

If you'd like to contribute to this sample, see [CONTRIBUTING.MD](/CONTRIBUTING.md).

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information, see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## More information

For more information, see ADAL.NET's conceptual documentation:

- [ADAL.NET's conceptual documentation](https://github.com/AzureAD/azure-activedirectory-library-for-dotnet/wiki)
- [Recommended pattern to acquire a token](https://github.com/AzureAD/azure-activedirectory-library-for-dotnet/wiki/AcquireTokenSilentAsync-using-a-cached-token#recommended-pattern-to-acquire-a-token)
- [Client credential flows](https://github.com/AzureAD/azure-activedirectory-library-for-dotnet/wiki/Client-credential-flows)
- [Using the acquired token to call a protected Web API](https://github.com/AzureAD/azure-activedirectory-library-for-dotnet/wiki/Using-the-acquired-token-to-call-a-protected-Web-API)
- [How to: Add app roles in your application and receive them in the token](https://docs.microsoft.com/en-us/azure/active-directory/develop/howto-add-app-roles-in-azure-ad-apps)

For more information about how OAuth 2.0 protocols work in this scenario and other scenarios, see [Authentication Scenarios for Azure AD](http://go.microsoft.com/fwlink/?LinkId=394414).

## FAQ

- [How to use a pre-existing certificate](https://github.com/Azure-Samples/active-directory-dotnet-daemon-certificate-credential/issues/29) instead of generating a self signed certificate.
