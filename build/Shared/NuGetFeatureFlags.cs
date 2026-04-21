// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using NuGet.Common;

namespace NuGet.Shared
{
    internal static class NuGetFeatureFlags
    {
        internal const string UsesNSJDeserializationSwitchName = "NuGet.UsesNSJDeserialization";
        internal const string UsesNSJDeserializationEnvVar = "NUGET_USES_NSJ_DESERIALIZATION";

        private static readonly Lazy<bool> _isNSJDeserializationEnabledByEnvironment =
            new Lazy<bool>(() => IsNSJDeserializationEnabledByEnvironment(EnvironmentVariableWrapper.Instance));

        /// <summary>Feature switch for NSJ deserialization. Defaults to <see langword="true"/>.</summary>
        [FeatureSwitchDefinition(UsesNSJDeserializationSwitchName)]
        internal static bool NSJDeserializationFeatureSwitch { get; } =
            !AppContext.TryGetSwitch(UsesNSJDeserializationSwitchName, out bool value) || value;

        /// <summary>Returns <see langword="false"/> when env var <c>NUGET_USES_NSJ_DESERIALIZATION</c> is <c>false</c>.</summary>
        internal static bool IsNSJDeserializationEnabledByEnvironment(IEnvironmentVariableReader? env = null)
        {
            if (env is null)
            {
                return _isNSJDeserializationEnabledByEnvironment.Value;
            }

            string? envValue = env.GetEnvironmentVariable(UsesNSJDeserializationEnvVar);
            return !string.Equals(envValue, "false", StringComparison.OrdinalIgnoreCase);
        }
    }
}
