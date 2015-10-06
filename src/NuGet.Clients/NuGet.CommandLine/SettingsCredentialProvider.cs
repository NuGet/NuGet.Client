using System;
using System.Globalization;
using System.Linq;
using System.Net;

namespace NuGet.CommandLine
{
    public class SettingsCredentialProvider : ICredentialProvider
    {
        private readonly Configuration.IPackageSourceProvider _packageSourceProvider;
        private readonly Logging.ILogger _logger;

        public SettingsCredentialProvider(Configuration.IPackageSourceProvider packageSourceProvider)
            : this(packageSourceProvider, Logging.NullLogger.Instance)
        {
        }

        public SettingsCredentialProvider(Configuration.IPackageSourceProvider packageSourceProvider, Logging.ILogger logger)
        {
            if (packageSourceProvider == null)
            {
                throw new ArgumentNullException(nameof(packageSourceProvider));
            }

            _packageSourceProvider = packageSourceProvider;
            _logger = logger;
        }

        public ICredentials GetCredentials(Uri uri, IWebProxy proxy, CredentialType credentialType, bool retrying)
        {
            NetworkCredential credentials;
            // If we are retrying, the stored credentials must be invalid.
            if (!retrying && (credentialType == CredentialType.RequestCredentials) && TryGetCredentials(uri, out credentials))
            {
                _logger.LogInformation(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        LocalizedResourceManager.GetString(nameof(NuGetResources.SettingsCredentials_UsingSavedCredentials)),
                        credentials.UserName));
                return credentials;
            }
            return null;
        }

        private bool TryGetCredentials(Uri uri, out NetworkCredential configurationCredentials)
        {
            var source = _packageSourceProvider.LoadPackageSources().FirstOrDefault(p =>
            {
                Uri sourceUri;
                return !String.IsNullOrEmpty(p.UserName)
                    && !String.IsNullOrEmpty(p.Password)
                    && Uri.TryCreate(p.Source, UriKind.Absolute, out sourceUri)
                    && UriEquals(sourceUri, uri);
            });
            if (source == null)
            {
                // The source is not in the config file
                configurationCredentials = null;
                return false;
            }
            configurationCredentials = new NetworkCredential(source.UserName, source.Password);
            return true;
        }

        /// <summary>
        /// Determines if the scheme, server and path of two Uris are identical.
        /// </summary>
        private static bool UriEquals(Uri uri1, Uri uri2)
        {
            uri1 = CreateODataAgnosticUri(uri1.OriginalString.TrimEnd('/'));
            uri2 = CreateODataAgnosticUri(uri2.OriginalString.TrimEnd('/'));

            return Uri.Compare(uri1, uri2, UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase) == 0;
        }

        // Bug 2379: SettingsCredentialProvider does not work
        private static Uri CreateODataAgnosticUri(string uri)
        {
            if (uri.EndsWith("$metadata", StringComparison.OrdinalIgnoreCase))
            {
                uri = uri.Substring(0, uri.Length - 9).TrimEnd('/');
            }
            return new Uri(uri);
        }
    }
}