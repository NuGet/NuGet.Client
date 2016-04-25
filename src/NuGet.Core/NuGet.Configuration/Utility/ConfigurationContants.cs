// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Configuration
{
    internal static class ConfigurationConstants
    {
        internal static readonly string DisabledPackageSources = "disabledPackageSources";

        internal static readonly string PackageSources = "packageSources";

        internal static readonly string DefaultPushSource = "DefaultPushSource";

        internal static readonly string PackageRestore = "packageRestore";

        internal static readonly string Config = "config";

        internal static readonly string enabled = "enabled";

        internal static readonly string ConfigurationDefaultsFile = "NuGetDefaults.config";

        internal static readonly string CredentialsSectionName = "packageSourceCredentials";

        internal static readonly string UsernameToken = "Username";

        internal static readonly string PasswordToken = "Password";

        internal static readonly string ClearTextPasswordToken = "ClearTextPassword";

        internal static readonly string ActivePackageSourceSectionName = "activePackageSource";

        internal static readonly string HostKey = "http_proxy";

        internal static readonly string UserKey = "http_proxy.user";

        internal static readonly string PasswordKey = "http_proxy.password";
        
        internal static readonly string NoProxy = "no_proxy";

        internal static readonly string KeyAttribute = "key";

        internal static readonly string ValueAttribute = "value";

        internal static readonly string ProtocolVersionAttribute = "protocolVersion";

    }
}
