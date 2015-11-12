// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Credentials
{
    /// <summary>
    /// This service manages orchestrates credential providers and supplies credentials
    /// for use in http requests
    /// </summary>
    public class CredentialService : ICredentialService
    {
        private readonly ConcurrentDictionary<string, bool> _retryCache
            = new ConcurrentDictionary<string, bool>();
        private readonly ConcurrentDictionary<string, CredentialResponse> _providerCredentialCache
            = new ConcurrentDictionary<string, CredentialResponse>();

        private readonly bool _nonInteractive;

        /// <summary>
        /// This semaphore ensures only one provider active per process, in order
        /// to prevent multiple concurrent interactive login dialogues.
        /// Unnamed semaphores are local to the current process.
        /// </summary>
        private static readonly Semaphore ProviderSemaphore = new Semaphore(1, 1);

        private Action<string> ErrorDelegate { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="providers">Enumeration of credential providers.</param>
        /// <param name="errorDelegate">Used to write error messages to the user</param>
        /// <param name="nonInteractive">If true, the nonInteractive flag will be passed to providers.
        /// NonInteractive requests must not promt the user for credentials.</param>
        public CredentialService(
            IEnumerable<ICredentialProvider> providers,
            Action<string> errorDelegate,
            bool nonInteractive)
        {
            if (errorDelegate == null)
            {
                throw new ArgumentNullException(nameof(errorDelegate));
            }

            ErrorDelegate = errorDelegate;
            _nonInteractive = nonInteractive;
            Providers = providers ?? new List<ICredentialProvider>();
        }

        /// <summary>
        /// Gets the currently configured providers.
        /// </summary>
        public IEnumerable<ICredentialProvider> Providers { get; } = null;

        /// <summary>
        /// Provides credentials for http requests.
        /// </summary>
        /// <param name="uri">The uri of a web resource for which credentials are needed.</param>
        /// <param name="proxy">The currently configured proxy. It may be necessary for CredentialProviders
        /// to use this proxy in order to acquire credentials from their authentication source.</param>
        /// <param name="isProxy">If true, get credentials to authenticate for the requested proxy.
        /// If false, the credentials are intended for a remote service.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A credential object, or null if no credentials could be acquired.</returns>
        public async Task<ICredentials> GetCredentials(Uri uri, IWebProxy proxy, bool isProxy,
            CancellationToken cancellationToken)
        {
            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            ICredentials creds = null;

            foreach (var provider in Providers)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var retryKey = RetryCacheKey(uri, isProxy, provider);
                var isRetry = _retryCache.ContainsKey(retryKey);

                try
                {
                    // This local semaphore ensures one provider active per process.
                    // We can consider other ways to allow more concurrency between providers, but need to
                    // ensure that only one interactive dialogue is ever presented at a time, and that
                    // providers are not writing shared resources.
                    // Since this service is called only when cached credentials are not available to the caller,
                    // such an optimization is likely not necessary.
                    ProviderSemaphore.WaitOne();

                    CredentialResponse response;
                    if (!TryFromCredentialCache(uri, isProxy, isRetry, provider, out response))
                    {
                        response = await provider.Get(uri, proxy, isProxyRequest: isProxy, isRetry: isRetry,
                            nonInteractive: _nonInteractive, cancellationToken: cancellationToken);

                        // Check that the provider gave us a valid response.
                        if (response == null || (response.Status != CredentialStatus.ProviderNotApplicable && 
                                                 response.Status != CredentialStatus.Success))
                        {
                            throw new ProviderException(Resources.ProviderException_MalformedResponse);
                        }


                        AddToCredentialCache(uri, isProxy, provider, response);
                    }

                    if (response.Status == CredentialStatus.Success)
                    {
                        if (response.Credentials == null)
                        {
                            // It is invalid to have a success without getting credentials.  that should
                            // instead be a failure. (or Provider not applicable if the endpoint is not
                            // one the provider works with).
                            throw new ProviderException(Resources.ProviderException_MalformedResponse);
                        }

                        _retryCache[retryKey] = true;
                        creds = response.Credentials;
                        break;
                    }
                }
                finally
                {
                    ProviderSemaphore.Release();
                }
            }

            return creds;
        }

        private bool TryFromCredentialCache(Uri uri, bool isProxy, bool isRetry, ICredentialProvider provider,
            out CredentialResponse credentials)
        {
            credentials = null;

            var key = CredentialCacheKey(uri, isProxy, provider);
            if (isRetry)
            {
                CredentialResponse removed;
                _providerCredentialCache.TryRemove(key, out removed);
                return false;
            }

            return _providerCredentialCache.TryGetValue(key, out credentials);
        }

        private void AddToCredentialCache(Uri uri, bool isProxy, ICredentialProvider provider,
            CredentialResponse credentials)
        {
            _providerCredentialCache[CredentialCacheKey(uri, isProxy, provider)] = credentials;
        }

        private static string RetryCacheKey(Uri uri, bool isProxy, ICredentialProvider provider)
        {
            return $"{provider.Id}_{isProxy}_{uri}";
        }

        private static string CredentialCacheKey(Uri uri, bool isProxy, ICredentialProvider provider)
        {
            var rootUri = GetRootUri(uri);
            return $"{provider.Id}_{isProxy}_{rootUri}";
        }

        private static Uri GetRootUri(Uri uri)
        {
            return new Uri(uri.GetComponents(UriComponents.SchemeAndServer, UriFormat.SafeUnescaped));
        }
    }
}
