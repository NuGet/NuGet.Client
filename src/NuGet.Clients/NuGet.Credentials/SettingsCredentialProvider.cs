using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;

namespace NuGet.Credentials
{
    // TODO: [jeffkl] this is just here because the old settingscredentialprovider needs a V2 adapter...
    public class SettingsCredentialProvider : ICredentialProvider
    {
        private readonly ILogger _logger;
        private readonly IPackageSourceProvider _packageSourceProvider;

        public SettingsCredentialProvider(IPackageSourceProvider packageSourceProvider)
            : this(packageSourceProvider, NullLogger.Instance)
        {
        }

        public SettingsCredentialProvider(IPackageSourceProvider packageSourceProvider, ILogger logger)
        {
            if (packageSourceProvider == null)
            {
                throw new ArgumentNullException(nameof(packageSourceProvider));
            }

            _packageSourceProvider = packageSourceProvider;
            _logger = logger;

            Id = $"{typeof(PluginCredentialProvider).Name}_{Guid.NewGuid()}";
        }

        public string Id { get; }

        public Task<CredentialResponse> GetAsync(Uri uri, IWebProxy proxy, CredentialRequestType type, string message, bool isRetry, bool nonInteractive, CancellationToken cancellationToken)
        {
            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var cred = GetCredentials(
                uri,
                proxy,
                type,
                isRetry);

            var response = cred != null
                ? new CredentialResponse(cred)
                : new CredentialResponse(CredentialStatus.ProviderNotApplicable);

            return Task.FromResult(response);
        }

        public ICredentials GetCredentials(Uri uri, IWebProxy proxy, CredentialRequestType type, bool retrying)
        {
            NetworkCredential credentials;
            // If we are retrying, the stored credentials must be invalid.
            if (!retrying && (type != CredentialRequestType.Proxy) && TryGetCredentials(uri, out credentials))
            {
                // TODO: [jeffkl] Bring back this log statement
                //_logger.LogMinimal(
                //    string.Format(
                //        CultureInfo.CurrentCulture,
                //        LocalizedResourceManager.GetString(nameof(NuGetResources.SettingsCredentials_UsingSavedCredentials)),
                //        credentials.UserName));
                return credentials;
            }

            return null;
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

        /// <summary>
        /// Determines if the scheme, server and path of two Uris are identical.
        /// </summary>
        private static bool UriEquals(Uri uri1, Uri uri2)
        {
            uri1 = CreateODataAgnosticUri(uri1.OriginalString.TrimEnd('/'));
            uri2 = CreateODataAgnosticUri(uri2.OriginalString.TrimEnd('/'));

            return Uri.Compare(uri1, uri2, UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase) == 0;
        }

        private bool TryGetCredentials(Uri uri, out NetworkCredential configurationCredentials)
        {
            var source = _packageSourceProvider.LoadPackageSources().FirstOrDefault(p =>
            {
                Uri sourceUri;
                return p.Credentials != null
                       && p.Credentials.IsValid()
                       && Uri.TryCreate(p.Source, UriKind.Absolute, out sourceUri)
                       && UriEquals(sourceUri, uri);
            });
            if (source == null)
            {
                // The source is not in the config file
                configurationCredentials = null;
                return false;
            }

            configurationCredentials = new NetworkCredential(source.Credentials.Username, source.Credentials.Password);
            return true;
        }
    }
}