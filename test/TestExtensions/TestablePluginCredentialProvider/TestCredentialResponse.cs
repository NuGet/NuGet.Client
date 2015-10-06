// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Test.TestExtensions.TestablePluginCredentialProvider
{
    public class TestCredentialResponse
    {
        public const string ResponseAbortMessage = "NUGET_TESTABLECREDENTIALPROVIDER_RESPONSEABORTMESSAGE";
        public const string ResponseDelaySeconds = "NUGET_TESTABLECREDENTIALPROVIDER_RESPONSEDELAYSECONDS";
        public const string ResponseExitCode = "NUGET_TESTABLECREDENTIALPROVIDER_RESPONSEEXITCODE";
        public const string ResponsePassword = "NUGET_TESTABLECREDENTIALPROVIDER_RESPONSEPASSWORD";
        public const string ResponseShouldThrow = "NUGET_TESTABLECREDENTIALPROVIDER_RESPONSESHOULDTHROW";
        public const string ResponseShouldAbort = "NUGET_TESTABLECREDENTIALPROVIDER_RESPONSESHOULDABORT";
        public const string ResponseUserName = "NUGET_TESTABLECREDENTIALPROVIDER_RESPONSEUSERNAME";

        public static void ClearAllEnvironmentVariables()
        {
            Environment.SetEnvironmentVariable(ResponseAbortMessage, string.Empty);
            Environment.SetEnvironmentVariable(ResponseDelaySeconds, string.Empty);
            Environment.SetEnvironmentVariable(ResponseExitCode, string.Empty);
            Environment.SetEnvironmentVariable(ResponsePassword, string.Empty);
            Environment.SetEnvironmentVariable(ResponseShouldThrow, string.Empty);
            Environment.SetEnvironmentVariable(ResponseShouldAbort, string.Empty);
            Environment.SetEnvironmentVariable(ResponseUserName, string.Empty);
        }

        public string Username { get; set; }

        public string Password { get; set; }

        public bool Abort { get; set; }

        public string AbortMessage { get; set; }

    }
}
