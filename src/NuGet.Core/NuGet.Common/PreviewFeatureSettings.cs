// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Common
{
    // TODO: Probably a better place to put this
    public static class PreviewFeatureSettings
    {
        internal static IEnvironmentVariableReader EnvironmentVariableReader { get; } = new EnvironmentVariableWrapper();

        public static bool DefaultCredentialsAfterCredentialProviders
        {
            get
            {
                bool flag;
                var flagString = EnvironmentVariableReader.GetEnvironmentVariable("NUGET_CREDENTIAL_PROVIDER_OVERRIDE_DEFAULT");
                return bool.TryParse(flagString, out flag) && flag;
            }
        }
    }
}
