// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using NuGet.Common;

namespace NuGet.Shared
{
    internal static class NuGetFeatureFlags
    {
        internal const string UseSystemTextJsonDeserializationSwitchName = "NuGet.UseSystemTextJsonDeserialization";
        internal const string UseSystemTextJsonDeserializationEnvVar = "NUGET_USE_SYSTEM_TEXT_JSON_DESERIALIZATION";

        static NuGetFeatureFlags()
        {
            StaticState.BuildEnded += ResetCache;
        }

        private static Lazy<bool> _isSystemTextJsonDeserializationDisabledByEnvironment =
            new Lazy<bool>(() => IsSystemTextJsonDeserializationDisabledByEnvironment(EnvironmentVariableWrapper.Instance));

        /// <summary>Discards the cached value so it is re-read from the environment on next use.</summary>
        internal static void ResetCache() =>
            _isSystemTextJsonDeserializationDisabledByEnvironment =
                new Lazy<bool>(() => IsSystemTextJsonDeserializationDisabledByEnvironment(EnvironmentVariableWrapper.Instance));

        /// <summary>
        /// Feature switch for System.Text.Json deserialization. The direct check allows the linker to trim the
        /// Newtonsoft.Json path when the switch is explicitly set to <see langword="true"/>.
        /// </summary>
        [FeatureSwitchDefinition(UseSystemTextJsonDeserializationSwitchName)]
        internal static bool UseSystemTextJsonDeserializationFeatureSwitch { get; } =
            AppContext.TryGetSwitch(UseSystemTextJsonDeserializationSwitchName, out bool value) && value;

        /// <summary>Returns <see langword="true"/> when env var <c>NUGET_USE_SYSTEM_TEXT_JSON_DESERIALIZATION</c> is <c>false</c>.</summary>
        /// <param name="env">
        /// Pass <see langword="null"/> (or omit) in production code to use the cached <see cref="Lazy{T}"/> value,
        /// avoiding repeated allocations on .NET Framework. Pass an explicit <see cref="IEnvironmentVariableReader"/>
        /// only in tests to override the value.
        /// </param>
        internal static bool IsSystemTextJsonDeserializationDisabledByEnvironment(IEnvironmentVariableReader? env = null)
        {
            if (env is null)
            {
                return _isSystemTextJsonDeserializationDisabledByEnvironment.Value;
            }

            string? envValue = env.GetEnvironmentVariable(UseSystemTextJsonDeserializationEnvVar);
            return string.Equals(envValue, "false", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Returns whether System.Text.Json is explicitly disabled by the feature switch or environment variable.</summary>
        internal static bool IsSystemTextJsonDeserializationDisabledByConfiguration(IEnvironmentVariableReader? env = null)
        {
            if (AppContext.TryGetSwitch(UseSystemTextJsonDeserializationSwitchName, out bool value))
            {
                return !value;
            }

            return IsSystemTextJsonDeserializationDisabledByEnvironment(env);
        }
    }
}
