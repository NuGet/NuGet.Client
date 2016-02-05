using System;
using System.Net;

namespace NuGet.Protocol
{
    /// <summary>
    /// A mutable ICredentials wrapper. This allows the underlying ICredentials to 
    /// be changed to work around HttpClientHandler not allowing Credentials to change.
    /// </summary>
    public class CredentialHelper : ICredentials
    {
        public ICredentials Credentials { get; set; }

        public NetworkCredential GetCredential(Uri uri, string authType)
        {
            // Credentials may change during this call so keep a local copy.
            var currentCredentials = Credentials;

            NetworkCredential result = null;

            if (currentCredentials == null)
            {
                result = new NetworkCredential();
            }
            else
            {
                result = currentCredentials.GetCredential(uri, authType);
            }

            return result;
        }
    }
}
