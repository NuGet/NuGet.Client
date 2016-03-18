// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Configuration
{
    public static class ConfigurationConstants
    {
        public static string ApiKeys = "apikeys";

        public static string DisabledPackageSources = "disabledPackageSources";

        public static string PackageSources = "packageSources";

        public static string DefaultPushSource = "DefaultPushSource";

        public static string PackageRestore = "packageRestore";

        public static string Config = "config";

        public static string Enabled = "enabled";

        public static string ConfigurationDefaultsFile = "NuGetDefaults.config";

        public static string CredentialsSectionName = "packageSourceCredentials";

        public static string UsernameToken = "Username";

        public static string PasswordToken = "Password";

        public static string ClearTextPasswordToken = "ClearTextPassword";

        public static string ActivePackageSourceSectionName = "activePackageSource";

        public static string HostKey = "http_proxy";

        public static string UserKey = "http_proxy.user";

        public static string PasswordKey = "http_proxy.password";
        
        public static string NoProxy     = "no_proxy";

        public static string KeyAttribute = "key";

        public static string ValueAttribute = "value";

        public static string ProtocolVersionAttribute = "protocolVersion";

        public static readonly string BeginIgnoreMarker = "NUGET: BEGIN LICENSE TEXT";
        public static readonly string EndIgnoreMarker = "NUGET: END LICENSE TEXT";
    }
}
