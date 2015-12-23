//----------------------------------------------------------------------------------------------
//    Copyright 2014 Microsoft Corporation
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
//----------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// The following using statements were added for this sample.
using System.Globalization;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System.Net.Http;
using System.Threading;
using System.Net.Http.Headers;
using System.Web.Script.Serialization;
using System.Security.Cryptography.X509Certificates;
using System.Configuration;
namespace TodoListDaemonWithCert
{
    class Program
    {
        //
        // The Client ID is used by the application to uniquely identify itself to Azure AD.
        // The Cert Name is the subject name of the certificate used to authenticate this application to Azure AD.
        // The Tenant is the name of the Azure AD tenant in which this application is registered.
        // The AAD Instance is the instance of Azure, for example public Azure or Azure China.
        // The Authority is the sign-in URL of the tenant.
        //
        private static string aadInstance = ConfigurationManager.AppSettings["ida:AADInstance"];
        private static string tenant = ConfigurationManager.AppSettings["ida:Tenant"];
        private static string clientId = ConfigurationManager.AppSettings["ida:ClientId"];
        private static string certName = ConfigurationManager.AppSettings["ida:CertName"];

        static string authority = String.Format(CultureInfo.InvariantCulture, aadInstance, tenant);

        //
        // To authenticate to the To Do list service, the client needs to know the service's App ID URI.
        // To contact the To Do list service we need it's URL as well.
        //
        private static string todoListResourceId = ConfigurationManager.AppSettings["todo:TodoListResourceId"];
        private static string todoListBaseAddress = ConfigurationManager.AppSettings["todo:TodoListBaseAddress"];

        private static HttpClient httpClient = new HttpClient();
        private static AuthenticationContext authContext = null;
        private static ClientAssertionCertificate certCred = null;

        static void Main(string[] args)
        {
            //
            // Create the authentication context to be used to acquire tokens.
            //
            authContext = new AuthenticationContext(authority);

            // Creating the cert - 'password' was used to generate the cert
            // http://blogs.msdn.com/b/exchangedev/archive/2015/01/22/building-demon-or-service-apps-with-office-365-mail-calendar-and-contacts-apis-oauth2-client-credential-flow.aspx
            //makecert - r - pe - n "CN=TodoListDaemonWithCert" - ss My - len 2048 TodoListDaemonWithCert.cer - sv TodoListDaemonPrivateKey.pvk
            //pvk2pfx - pvk TodoListDaemonPrivateKey.pvk - spc TodoListDaemonWithCert.cer - pfx TodoListDaemonWithCert.pfx - po password
            //$cer = New - Object System.Security.Cryptography.X509Certificates.X509Certificate2
            //$cer.Import("TodoListDaemonWithCert.cer")
            //$bin = $cer.GetRawCertData()
            //$base64Value = [System.Convert]::ToBase64String($bin)
            //$bin = $cer.GetCertHash()
            //$base64Thumbprint = [System.Convert]::ToBase64String($bin)
            //$keyid = [System.Guid]::NewGuid().ToString()

            // To access cert store Run --> certmgr.msc

            // Uploaded to Manifest using $base64Thumbprint, $keyid, $base64Value

            //
            // Initialize the Certificate Credential to be used by ADAL.
            // First find the matching certificate in the cert store.
            //

            // Attempt to load from file directly
            //var x509Certificate = new X509Certificate2("~/TodoListDaemonWithCert.pfx", "password", X509KeyStorageFlags.Exportable);
            //byte[] bytes = x509Certificate.Export(X509ContentType.Pkcs12, "password");
            //certCred = new ClientAssertionCertificate(clientId, bytes, "password");

            // Attempt to load from My cert store
            X509Certificate2 cert = null;
            X509Store store = new X509Store(StoreLocation.CurrentUser);
            try
            {
                store.Open(OpenFlags.ReadOnly);
                // Place all certificates in an X509Certificate2Collection object.
                X509Certificate2Collection certCollection = store.Certificates;
                // Find unexpired certificates.
                X509Certificate2Collection currentCerts = certCollection.Find(X509FindType.FindByTimeValid, DateTime.Now, false);
                // From the collection of unexpired certificates, find the ones with the correct name.
                X509Certificate2Collection signingCert = currentCerts.Find(X509FindType.FindBySubjectDistinguishedName, certName, false);
                if (signingCert.Count == 0)
                {
                    // No matching certificate found.
                    return;
                }
                // Return the first certificate in the collection, has the right name and is current.
                cert = signingCert[0];
            }
            finally
            {
                store.Close();
            }

            // Then create the certificate credential.
            certCred = new ClientAssertionCertificate(clientId, cert.Export(X509ContentType.Pkcs12, "password"), "password");

            //
            // Call the To Do service 10 times with short delay between calls.
            //
            for (int i = 0; i < 10; i++)
            {
                Thread.Sleep(3000);
                PostTodo().Wait();
                Thread.Sleep(3000);
                GetTodo().Wait();
            }
        }

        static async Task PostTodo()
        {
            //
            // Get an access token from Azure AD using client credentials.
            // If the attempt to get a token fails because the server is unavailable, retry twice after 3 seconds each.
            //
            AuthenticationResult result = null;
            int retryCount = 0;
            bool retry = false;

            do
            {
                retry = false;
                try
                {   // ADAL includes an in memory cache, so this call will only send a message to the server if the cached token is expired.
                    result = await authContext.AcquireTokenAsync(todoListResourceId, certCred);
                }
                catch (Exception ex)
                {
                    AdalException exc = ex as AdalException;
                    if (exc.ErrorCode == "temporarily_unavailable")
                    {
                        retry = true;
                        retryCount++;
                        Thread.Sleep(3000);
                    }

                    Console.WriteLine(
                        String.Format("An error occurred while acquiring a token\nTime: {0}\nError: {1}\nRetry: {2}\n",
                        DateTime.Now.ToString(),
                        ex.ToString(),
                        retry.ToString()));
                }

            } while ((retry == true) && (retryCount < 3));

            if (result == null)
            {
                Console.WriteLine("Canceling attempt to contact To Do list service.\n");
                return;
            }

            //
            // Post an item to the To Do list service.
            //

            // Add the access token to the authorization header of the request.
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);

            // Forms encode To Do item and POST to the todo list web api.
            string timeNow = DateTime.Now.ToString();
            Console.WriteLine("Posting to To Do list at {0}", timeNow);
            string todoText = "Task at time: " + timeNow;
            HttpContent content = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("Title", todoText) });
            HttpResponseMessage response = await httpClient.PostAsync(todoListBaseAddress + "/api/todolist", content);

            if (response.IsSuccessStatusCode == true)
            {
                Console.WriteLine("Successfully posted new To Do item:  {0}\n", todoText);
            }
            else
            {
                Console.WriteLine("Failed to post a new To Do item\nError:  {0}\n", response.ReasonPhrase);
            }
        }

        static async Task GetTodo()
        {
            //
            // Get an access token from Azure AD using client credentials.
            // If the attempt to get a token fails because the server is unavailable, retry twice after 3 seconds each.
            //
            AuthenticationResult result = null;
            int retryCount = 0;
            bool retry = false;

            do
            {
                retry = false;
                try
                {
                    // ADAL includes an in memory cache, so this call will only send a message to the server if the cached token is expired.
                    result = await authContext.AcquireTokenAsync(todoListResourceId, certCred);
                }
                catch (AdalException ex)
                {
                    if (ex.ErrorCode == "temporarily_unavailable")
                    {
                        retry = true;
                        retryCount++;
                        Thread.Sleep(3000);
                    }

                    Console.WriteLine(
                        String.Format("An error occurred while acquiring a token\nTime: {0}\nError: {1}\nRetry: {2}\n",
                        DateTime.Now.ToString(),
                        ex.ToString(),
                        retry.ToString()));
                }

            } while ((retry == true) && (retryCount < 3));

            if (result == null)
            {
                Console.WriteLine("Canceling attempt to contact To Do list service.\n");
                return;
            }

            //
            // Read items from the To Do list service.
            //

            // Add the access token to the authorization header of the request.
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);

            // Call the To Do list service.
            Console.WriteLine("Retrieving To Do list at {0}", DateTime.Now.ToString());
            HttpResponseMessage response = await httpClient.GetAsync(todoListBaseAddress + "/api/todolist");

            if (response.IsSuccessStatusCode)
            {
                // Read the response and output it to the console.
                string s = await response.Content.ReadAsStringAsync();
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                List<TodoItem> toDoArray = serializer.Deserialize<List<TodoItem>>(s);

                int count = 0;
                foreach (TodoItem item in toDoArray)
                {
                    Console.WriteLine(item.Title);
                    count++;
                }

                Console.WriteLine("Total item count:  {0}\n", count);
            }
            else
            {
                Console.WriteLine("Failed to retrieve To Do list\nError:  {0}\n", response.ReasonPhrase);
            }
        }
    }
}
