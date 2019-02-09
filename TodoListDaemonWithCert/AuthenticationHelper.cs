/*
 The MIT License (MIT)

Copyright (c) 2018 Microsoft Corporation

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ADAL = Microsoft.IdentityModel.Clients.ActiveDirectory;
using MSAL = Microsoft.Identity.Client;

namespace TodoListDaemonWithCert
{
    public class AuthenticationHelper
    {
        private ADAL.AuthenticationContext authContext = null;
        private ADAL.ClientAssertionCertificate certCredAdal = null;
        private MSAL.ConfidentialClientApplication confidentialClientApplication;
        private MSAL.ClientAssertionCertificate certCredMsal = null;

        public AuthenticationHelper()
        {
            Initialize();
        }

        private void Initialize()
        {
            // Create the authentication context to be used to acquire tokens.
            authContext = new ADAL.AuthenticationContext(ConfigurationHelper.authority);
            // Initialize the Certificate Credential to be used by ADAL.
            X509Certificate2 cert = CertificateHelper.ReadCertificateFromStore(ConfigurationHelper.certName);
            if (cert == null)
            {
                Console.WriteLine($"Cannot find active certificate '{ConfigurationHelper.certName}' in certificates for current user. Please check configuration");
                throw new AuthenticationHelperException(-1);
            }

            // Then create the certificate credential client assertion.
            certCredAdal = new ADAL.ClientAssertionCertificate(ConfigurationHelper.clientId, cert);

            certCredMsal = new MSAL.ClientAssertionCertificate(cert);
            MSAL.ClientCredential clientCredential = new MSAL.ClientCredential(certCredMsal);
            confidentialClientApplication = new MSAL.ConfidentialClientApplication(ConfigurationHelper.clientId, ConfigurationHelper.authority, "https://daemon", clientCredential, new MSAL.TokenCache(), new MSAL.TokenCache());
        }

        /// <summary>
        /// Get an access token from Azure AD using client credentials.
        /// If the attempt to get a token fails because the server is unavailable, retry twice after 3 seconds each
        /// </summary>
        public async Task<AuthenticationResult> GetAccessToken(string todoListResourceId)
        {
            //
            // Get an access token from Azure AD using client credentials.
            // If the attempt to get a token fails because the server is unavailable, retry twice after 3 seconds each.
            //
            AuthenticationResult authenticationResult = new AuthenticationResult();

            int retryCount = 0;
            bool retry = false;
            int errorCode = 0;

            do
            {
                retry = false;
                errorCode = 0;

                try
                {
                    // ADAL includes an in memory cache, so this call will only send a message to the server if the cached token is expired.
                    authenticationResult.AdalResult = await authContext.AcquireTokenAsync(todoListResourceId, certCredAdal);
                }
                catch (ADAL.AdalException ex)
                {
                    if (ex.ErrorCode == "temporarily_unavailable")
                    {
                        retry = true;
                        retryCount++;
                        Thread.Sleep(3000);
                    }

                    Console.WriteLine(
                        String.Format("ADAL. An error occurred while acquiring a token\nTime: {0}\nError: {1}\nRetry: {2}\n",
                        DateTime.Now.ToString(),
                        ex.ToString(),
                        retry.ToString()));

                    errorCode = -1;
                }

                try
                {
                    authenticationResult.MsalResult = await confidentialClientApplication.AcquireTokenForClientAsync(
                        new List<string> { $"{todoListResourceId}/.default" }
                        );
                }
                catch (MSAL.MsalException ex)
                {
                    if (ex.ErrorCode == "temporarily_unavailable")
                    {
                        retry = true;
                        retryCount++;
                        Thread.Sleep(3000);
                    }

                    Console.WriteLine(
                        String.Format("MSAL. An error occurred while acquiring a token\nTime: {0}\nError: {1}\nRetry: {2}\n",
                        DateTime.Now.ToString(),
                        ex.ToString(),
                        retry.ToString()));

                    errorCode = -1;
                }

            } while ((retry == true) && (retryCount < 3));

            if (errorCode != 0)
            {
                throw new AuthenticationHelperException(-1);
            }

            return authenticationResult;
        }

    }
}
