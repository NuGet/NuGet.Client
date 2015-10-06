// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Credentials
{
    /// <summary>
    /// Response data returned from plugin credential provider applications
    /// </summary>
    public class PluginCredentialResponse
    {
        public string Username { get; set; }

        public string Password { get; set; }

        /// <summary>
        /// Setting this flag to true indicates that the credential provider is
        /// the correct provider for the given Uri, but is unable to provide credentials.
        /// Setting abort will result in the current WebRequest to fail and no further credential providers
        /// will be queried.
        /// </summary>
        public bool Abort { get; set; }

        public string AbortMessage { get; set; }

        public bool IsValid => !String.IsNullOrWhiteSpace(Username) || !String.IsNullOrWhiteSpace(Password);
    }
}
