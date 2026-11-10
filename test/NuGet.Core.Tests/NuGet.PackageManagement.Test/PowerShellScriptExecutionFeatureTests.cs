// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Shared;
using Test.Utility;
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
        [InlineData("0", true, false)]
        [InlineData("invalid", false, false)]
        [InlineData("invalid", true, true)]
        public void IsEnabledCore_WithEnvironmentOverrideAndAppContext_ReturnsExpectedState(
            string? value, bool switchEnabled, bool expected)
        {
            var environment = new TestEnvironmentVariableReader(new Dictionary<string, string?>
            {
                { PowerShellScriptExecutionFeature.EnvironmentVariable, value }
            });
            AppContext.TryGetSwitch(PowerShellScriptExecutionFeature.SwitchName, out bool originalSwitch);
            try
            {
                AppContext.SetSwitch(PowerShellScriptExecutionFeature.SwitchName, switchEnabled);
                bool actual = PowerShellScriptExecutionFeature.IsEnabledCore(environment);

                Assert.Equal(expected, actual);
            }
            finally
            {
                AppContext.SetSwitch(PowerShellScriptExecutionFeature.SwitchName, originalSwitch);
            }
        }

        [Fact]
        public void IsEnabledCore_WhenAppContextSwitchChanges_ReturnsCurrentState()
        {
            var environment = new TestEnvironmentVariableReader(new Dictionary<string, string>());
            AppContext.TryGetSwitch(PowerShellScriptExecutionFeature.SwitchName, out bool originalSwitch);
            try
            {
                AppContext.SetSwitch(PowerShellScriptExecutionFeature.SwitchName, false);
                Assert.False(PowerShellScriptExecutionFeature.IsEnabledCore(environment));

                AppContext.SetSwitch(PowerShellScriptExecutionFeature.SwitchName, true);
                Assert.True(PowerShellScriptExecutionFeature.IsEnabledCore(environment));

                AppContext.SetSwitch(PowerShellScriptExecutionFeature.SwitchName, false);
                Assert.False(PowerShellScriptExecutionFeature.IsEnabledCore(environment));
            }
            finally
            {
                AppContext.SetSwitch(PowerShellScriptExecutionFeature.SwitchName, originalSwitch);
            }
        }
    }
}
