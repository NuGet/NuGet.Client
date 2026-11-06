// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Shared;
using Xunit;

namespace NuGet.PackageManagement.Test
{
    [Collection(nameof(NotThreadSafeResourceCollection))]
    public class PowerShellScriptExecutionFeatureTests
    {
        [Theory]
        [InlineData(null, false, false)]
        [InlineData(null, true, true)]
        [InlineData("1", false, true)]
        [InlineData("1", true, true)]
        [InlineData("0", false, false)]
        [InlineData("0", true, false)]
        [InlineData("invalid", false, false)]
        [InlineData("invalid", true, true)]
        [InlineData("true", false, false)]
        [InlineData("false", true, true)]
        public void IsEnabled_EnvironmentOverridesAppContext(string? value, bool switchEnabled, bool expected)
        {
            using var environment = new PowerShellScriptExecutionFeatureScope(value);
            AppContext.TryGetSwitch(PowerShellScriptExecutionFeature.SwitchName, out bool originalSwitch);
            try
            {
                AppContext.SetSwitch(PowerShellScriptExecutionFeature.SwitchName, switchEnabled);
                Assert.Equal(expected, PowerShellScriptExecutionFeature.IsEnabled);
                AppContext.SetSwitch(PowerShellScriptExecutionFeature.SwitchName, !switchEnabled);
                Assert.Equal(value == "1" || (value != "0" && !switchEnabled), PowerShellScriptExecutionFeature.IsEnabled);
            }
            finally
            {
                AppContext.SetSwitch(PowerShellScriptExecutionFeature.SwitchName, originalSwitch);
            }
        }
    }

    internal sealed class PowerShellScriptExecutionFeatureScope : IDisposable
    {
        private readonly string? _originalValue;

        internal PowerShellScriptExecutionFeatureScope(string? value)
        {
            _originalValue = Environment.GetEnvironmentVariable(PowerShellScriptExecutionFeature.EnvironmentVariable);
            Environment.SetEnvironmentVariable(PowerShellScriptExecutionFeature.EnvironmentVariable, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(PowerShellScriptExecutionFeature.EnvironmentVariable, _originalValue);
        }
    }
}
