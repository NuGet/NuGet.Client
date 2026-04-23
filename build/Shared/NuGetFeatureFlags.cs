// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using NuGet.Common;

namespace NuGet.Shared
{
    internal static class NuGetFeatureFlags
    {
        internal const string UseLegacyJsonDeserializationSwitchName = "NuGet.UseLegacyJsonDeserialization";
        internal const string UseLegacyJsonDeserializationEnvVar = "NUGET_USE_LEGACY_JSON_DESERIALIZATION";

        private static readonly Lazy<bool> _isLegacyJsonDeserializationEnabledByEnvironment =
            new Lazy<bool>(() => IsLegacyJsonDeserializationEnabledByEnvironment(EnvironmentVariableWrapper.Instance));

        /// <summary>Feature switch for legacy (Newtonsoft) JSON deserialization. Defaults to <see langword="false"/> (STJ is the default).</summary>
        [FeatureSwitchDefinition(UseLegacyJsonDeserializationSwitchName)]
        internal static bool UseLegacyJsonDeserializationFeatureSwitch { get; } =
            AppContext.TryGetSwitch(UseLegacyJsonDeserializationSwitchName, out bool value) && value;

        /// <summary>Returns <see langword="true"/> when env var <c>NUGET_USE_LEGACY_JSON_DESERIALIZATION</c> is <c>true</c>.</summary>
        /// <param name="env">
        /// Pass <see langword="null"/> (or omit) in production code to use the cached <see cref="Lazy{T}"/> value,
        /// avoiding repeated allocations on .NET Framework. Pass an explicit <see cref="IEnvironmentVariableReader"/>
        /// only in tests to override the value.
        /// </param>
        internal static bool IsLegacyJsonDeserializationEnabledByEnvironment(IEnvironmentVariableReader? env = null)
        {
            if (env is null)
            {
                return _isLegacyJsonDeserializationEnabledByEnvironment.Value;
            }

            string? envValue = env.GetEnvironmentVariable(UseLegacyJsonDeserializationEnvVar);
            return string.Equals(envValue, "true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
