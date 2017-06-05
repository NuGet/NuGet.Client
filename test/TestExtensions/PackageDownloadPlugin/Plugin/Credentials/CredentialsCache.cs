// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Concurrent;
using System.Net;

namespace NuGet.Test.PackageDownloadPlugin
{
    internal sealed class CredentialsCache
    {
        private readonly ConcurrentDictionary<string, NetworkCredential> _credentials;

        internal CredentialsCache()
        {
            _credentials = new ConcurrentDictionary<string, NetworkCredential>();
        }

        internal NetworkCredential GetCredential(string url)
        {
            NetworkCredential credential;

            if (!_credentials.TryGetValue(url, out credential))
            {
                credential = new NetworkCredential();
            }

            return credential;
        }

        internal void UpdateCredential(string url, NetworkCredential credential)
        {
            _credentials.AddOrUpdate(url, credential, (u, old) => credential);
        }
    }
}