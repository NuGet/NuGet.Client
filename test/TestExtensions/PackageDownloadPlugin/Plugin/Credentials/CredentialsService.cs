// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Protocol.Plugins;

namespace NuGet.Test.PackageDownloadPlugin
{
    internal sealed class CredentialsService : ICredentialService
    {
        private const string _basicAuthenticationType = "Basic";

        private IPlugin _plugin;

        internal CredentialsCache PackageSourceCredentials { get; }
        internal CredentialsCache ProxyCredentials { get; }

        public bool HandlesDefaultCredentials => true;

        internal CredentialsService()
        {
            PackageSourceCredentials = new CredentialsCache();
            ProxyCredentials = new CredentialsCache();
        }

        public async Task<ICredentials> GetCredentialsAsync(
            Uri uri,
            IWebProxy proxy,
            CredentialRequestType type,
            string message,
            CancellationToken cancellationToken)
        {
            Assert.IsNotNull(uri, nameof(uri));

            cancellationToken.ThrowIfCancellationRequested();

            var credential = GetCredentialFromCache(uri, type);
            var authType = GetAuthenticationType(type);

            if (IsValid(credential))
            {
                return credential.GetCredential(uri, authType);
            }

            var statusCode = GetHttpStatusCode(type);

            var response = await _plugin.Connection.SendRequestAndReceiveResponseAsync<GetCredentialsRequest, GetCredentialsResponse>(
                MessageMethod.GetCredentials,
                new GetCredentialsRequest(uri.AbsoluteUri, statusCode),
                cancellationToken);

            if (response != null && response.ResponseCode == MessageResponseCode.Success)
            {
                credential = new NetworkCredential(response.Username, response.Password);

                UpdateCredentialInCache(uri, type, credential);

                return credential.GetCredential(uri, authType);
            }

            return null;
        }

        public bool TryGetLastKnownGoodCredentialsFromCache(
            Uri uri,
            bool isProxy,
            out ICredentials credentials)
        {
            throw new NotImplementedException();
        }

        internal void SetPlugin(IPlugin plugin)
        {
            Assert.IsNotNull(plugin, nameof(plugin));

            _plugin = plugin;
        }

        private void UpdateCredentialInCache(Uri uri, CredentialRequestType type, NetworkCredential credential)
        {
            switch (type)
            {
                case CredentialRequestType.Proxy:
                    ProxyCredentials.UpdateCredential(uri.AbsoluteUri, credential);
                    break;

                case CredentialRequestType.Unauthorized:
                case CredentialRequestType.Forbidden:
                    PackageSourceCredentials.UpdateCredential(uri.AbsoluteUri, credential);
                    break;

                default:
                    throw new NotImplementedException();
            }
        }

        private string GetAuthenticationType(CredentialRequestType type)
        {
            switch (type)
            {
                case CredentialRequestType.Proxy:
                    return _basicAuthenticationType;

                case CredentialRequestType.Unauthorized:
                case CredentialRequestType.Forbidden:
                    return null;

                default:
                    throw new NotImplementedException();
            }
        }

        private NetworkCredential GetCredentialFromCache(Uri uri, CredentialRequestType type)
        {
            switch (type)
            {
                case CredentialRequestType.Proxy:
                    return ProxyCredentials.GetCredential(uri.AbsoluteUri);

                case CredentialRequestType.Unauthorized:
                case CredentialRequestType.Forbidden:
                    return PackageSourceCredentials.GetCredential(uri.AbsoluteUri);

                default:
                    throw new NotImplementedException();
            }
        }

        private static HttpStatusCode GetHttpStatusCode(CredentialRequestType requestType)
        {
            switch (requestType)
            {
                case CredentialRequestType.Proxy:
                    return HttpStatusCode.ProxyAuthenticationRequired;

                case CredentialRequestType.Unauthorized:
                    return HttpStatusCode.Unauthorized;

                case CredentialRequestType.Forbidden:
                    return HttpStatusCode.Forbidden;

                default:
                    throw new NotImplementedException();
            }
        }

        private static bool IsValid(NetworkCredential credential)
        {
            return !string.IsNullOrEmpty(credential.UserName)
                && !string.IsNullOrEmpty(credential.Password);
        }
    }
}