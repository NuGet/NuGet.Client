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

        public string Message { get; set; }

        public bool IsValid => !String.IsNullOrWhiteSpace(Username) || !String.IsNullOrWhiteSpace(Password);
    }

    public enum PluginCredentialResponseExitCode
    {
        Success = 0,
        ProviderNotApplicable = 1,
        Failure = 2
    }
}
