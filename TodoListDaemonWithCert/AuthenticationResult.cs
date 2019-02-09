using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ADAL = Microsoft.IdentityModel.Clients.ActiveDirectory;
using MSAL = Microsoft.Identity.Client;

namespace TodoListDaemonWithCert
{
    public class AuthenticationResult
    {
        public ADAL.AuthenticationResult AdalResult { get; set; }
        public MSAL.AuthenticationResult MsalResult { get; set; }
    }
}
