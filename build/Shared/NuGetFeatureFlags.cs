// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using NuGet.Common;

namespace NuGet.Shared
{
    internal static class NuGetFeatureFlags
    {
        internal const string UseNSJDeserializationSwitchName = "NuGet.UseNSJDeserialization";
        internal const string UseNSJDeserializationEnvVar = "NUGET_USE_NSJ_DESERIALIZATION";

        private static readonly Lazy<bool> _isNSJDeserializationEnabledByEnvironment =
            new Lazy<bool>(() => IsNSJDeserializationEnabledByEnvironment(EnvironmentVariableWrapper.Instance));

        /// <summary>Feature switch for NSJ deserialization. Defaults to <see langword="false"/> (STJ is the default).</summary>
        [FeatureSwitchDefinition(UseNSJDeserializationSwitchName)]
        internal static bool UseNSJDeserializationFeatureSwitch { get; } =
            AppContext.TryGetSwitch(UseNSJDeserializationSwitchName, out bool value) && value;

        /// <summary>Returns <see langword="true"/> when env var <c>NUGET_USE_NSJ_DESERIALIZATION</c> is <c>true</c>.</summary>
        internal static bool IsNSJDeserializationEnabledByEnvironment(IEnvironmentVariableReader? env = null)
        {
            if (env is null)
            {
                return _isNSJDeserializationEnabledByEnvironment.Value;
            }

            string? envValue = env.GetEnvironmentVariable(UseNSJDeserializationEnvVar);
            return string.Equals(envValue, "true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
