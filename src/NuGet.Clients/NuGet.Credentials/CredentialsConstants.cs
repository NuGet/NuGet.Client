// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Credentials
{
    public class CredentialsConstants
    {
        public const string ExtensionsPathEnvar = "NUGET_EXTENSIONS_PATH";

        public const string PluginPrefixSetting = "CredentialProvider.Plugin.";

        public const int ProviderTimeoutSecondsDefault = 300;

        public const string ProviderTimeoutSecondsEnvar = "NUGET_CREDENTIAL_PROVIDER_TIMEOUT_SECONDS";

        public const string ProviderTimeoutSecondsSetting = "CredentialProvider.Timeout";
        
        public const string SettingsConfigSection = "config";

    }
}
