// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Security;

namespace NuGet.Test.TestExtensions.TestableVSCredentialProvider
{
    public sealed class TestCredentials
        : ICredentials
    {
        private readonly string _username;
        private readonly SecureString _token;

        internal TestCredentials(string username, SecureString token)
        {
            _username = username;
            _token = token;
        }

        public NetworkCredential GetCredential(Uri uri, string authType)
        {
            return new NetworkCredential(_username, _token);
        }
    }
}
