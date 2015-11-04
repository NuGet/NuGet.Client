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
        private static IEnumerable<ICredentialProvider> _defaultProviders
            = new List<ICredentialProvider>();

        private IEnumerable<ICredentialProvider> _providerList = null;

        private readonly ConcurrentDictionary<string, bool> _retryCache
            = new ConcurrentDictionary<string, bool>();
        private readonly ConcurrentDictionary<string, ICredentials> _providerCredentialCache
            = new ConcurrentDictionary<string, ICredentials>();

        private readonly bool _nonInteractive;
        private readonly bool _useCache;
        /// <summary>
        /// This semaphore ensures only one provider active per process, in order
        /// to prevent multiple concurrent interactive login dialogues.
        /// Unnamed semaphores are local to the current process.
        /// </summary>
        private readonly Semaphore _providerSemaphore = new Semaphore(1, 1);

        private Action<string> ErrorDelegate { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="errorDelegate">Used to write error messages to the user</param>
        /// <param name="nonInteractive">If true, the nonInteractive flag will be passed to providers.
        /// NonInteractive requests must not promt the user for credentials.</param>
        /// <param name="useCache">If true, maintain a cache of credentials per provider. This is set
        /// to true by the Visual Studio Extension, which initiates requests in parallel
        /// in multi-project builds, and for which we do not want multiple credential prompts.</param>
        public CredentialService(Action<string> errorDelegate, bool nonInteractive, bool useCache)
        {
            if (errorDelegate == null)
            {
                throw new ArgumentNullException(nameof(errorDelegate));
            }

            ErrorDelegate = errorDelegate;
            _nonInteractive = nonInteractive;
            _useCache = useCache;
        }

        /// <summary>
        /// New CredentialService objects will be populated with these default
        /// configured providers.
        /// </summary>
        public static IEnumerable<ICredentialProvider> DefaultProviders
        {
            set
            {
                _defaultProviders = value;
            }
            internal get { return _defaultProviders;}
        }

        /// <summary>
        /// Gets the currently configured providers.
        /// Internal set is provided for use by unit tests.
        /// </summary>
        public IEnumerable<ICredentialProvider> Providers
        {
            get { return _providerList ?? _defaultProviders;}
            internal set { _providerList =  value; }
        } 

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

            ICredentials response = null;

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
                    _providerSemaphore.WaitOne();

                    if (!TryFromCredentialCache(uri, isProxy, isRetry, provider, out response))
                    {
                        response = await provider.Get(uri, proxy, isProxyRequest: isProxy, isRetry: isRetry,
                            nonInteractive: _nonInteractive, cancellationToken: cancellationToken);
                    }

                    if (response != null)
                    {
                        AddToCredentialCache(uri, isProxy, provider, response);
                        _retryCache[retryKey] = true;
                        break;
                    }
                }
                finally
                {
                    _providerSemaphore.Release();
                }
            }

            return response;
        }

        private bool TryFromCredentialCache(Uri uri, bool isProxy, bool isRetry, ICredentialProvider provider,
            out ICredentials credentials)
        {
            credentials = null;

            if (!_useCache)
            {
                return false;
            }

            var key = CredentialCacheKey(uri, isProxy, provider);
            if (isRetry)
            {
                ICredentials removed;
                _providerCredentialCache.TryRemove(key, out removed);
                return false;
            }

            return _providerCredentialCache.TryGetValue(key, out credentials);
        }

        private void AddToCredentialCache(Uri uri, bool isProxy, ICredentialProvider provider,
            ICredentials credentials)
        {
            if (!_useCache || credentials == null)
            {
                return;
            }

            _providerCredentialCache[CredentialCacheKey(uri, isProxy, provider)] = credentials;
        }

        private static string RetryCacheKey(Uri uri, bool isProxy, ICredentialProvider provider)
        {
            return $"{provider.GetType().FullName}_{isProxy}_{uri}";
        }

        private static string CredentialCacheKey(Uri uri, bool isProxy, ICredentialProvider provider)
        {
            var rootUri = GetRootUri(uri);
            return $"{provider.GetType().FullName}_{isProxy}_{rootUri}";
        }

        private static Uri GetRootUri(Uri uri)
        {
            return new Uri(uri.GetComponents(UriComponents.SchemeAndServer, UriFormat.SafeUnescaped));
        }
    }
}
