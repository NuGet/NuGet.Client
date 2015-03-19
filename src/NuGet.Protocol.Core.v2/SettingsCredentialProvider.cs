using System;
using System.Linq;
using System.Net;
using NuGet.Configuration;

namespace NuGet.Protocol.Core.v2
{
    public class SettingsCredentialProvider : ICredentialProvider
    {
        private Configuration.PackageSource _source;

        public SettingsCredentialProvider(Configuration.PackageSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            _source = source;
        }

        public ICredentials GetCredentials(Uri uri, IWebProxy proxy, CredentialType credentialType, bool retrying)
        {
            NetworkCredential credentials;
            // If we are retrying, the stored credentials must be invalid. 
            if (!retrying && (credentialType == CredentialType.RequestCredentials) && TryGetCredentials(out credentials))
            {
                return credentials;
            }
            return null;
        }

        private bool TryGetCredentials(out NetworkCredential configurationCredentials)
        {
            if (String.IsNullOrEmpty(_source.UserName) || String.IsNullOrEmpty(_source.Password))
            {
                // The source is not in the config file
                configurationCredentials = null;
                return false;
            }
            configurationCredentials = new NetworkCredential(_source.UserName, _source.Password);
            return true;
        }
    }
}
