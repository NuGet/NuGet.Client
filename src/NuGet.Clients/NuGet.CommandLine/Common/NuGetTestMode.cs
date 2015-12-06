// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Common
{
    // Note: we may delete this logic once we no longer depend on NuGet.Core's PackageServer type,
    // as it is embedded in NuGet.Protocol.Core.Types.UserAgent.
    internal static class NuGetTestMode
    {
        private const string _testModeEnvironmentVariableName = "NuGetTestModeEnabled";
        public const string NuGetTestClientName = "NuGet Test Client";

        public static bool Enabled
        {
            get
            {
                var testMode = System.Environment.GetEnvironmentVariable(_testModeEnvironmentVariableName);
                if (string.IsNullOrEmpty(testMode))
                {
                    return false;
                }

                bool isEnabled;
                return bool.TryParse(testMode, out isEnabled) && isEnabled;
            }
        }
    }
}