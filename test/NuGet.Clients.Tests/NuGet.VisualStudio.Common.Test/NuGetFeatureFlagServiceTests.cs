// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Internal.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Sdk.TestFramework;
using Microsoft.VisualStudio.Shell;
using Moq;
using NuGet.Common;
using NuGet.Shared;
using Test.Utility;
using Xunit;

namespace NuGet.VisualStudio.Common.Test
{
    [Collection(MockedVS.Collection)]
    public class NuGetFeatureFlagServiceTests
    {
        private GlobalServiceProvider _globalProvider;
        private readonly INuGetExperimentationService _experimentationService = Mock.Of<INuGetExperimentationService>();

        public NuGetFeatureFlagServiceTests(GlobalServiceProvider sp)
        {
            sp.Reset();
            _globalProvider = sp;
            NuGetUIThreadHelper.SetCustomJoinableTaskFactory(ThreadHelper.JoinableTaskFactory);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task InitializeAsync_PublishesPowerShellScriptExecutionFlag(bool flagEnabled)
        {
            var featureFlags = new Mock<IVsFeatureFlags>(MockBehavior.Strict);
            featureFlags.Setup(x => x.IsFeatureEnabled("NuGet.ControlPowerShellScriptExecution", false)).Returns(flagEnabled);
            _globalProvider.AddService(typeof(SVsFeatureFlags), featureFlags.Object);
            var service = new NuGetFeatureFlagService(
                TestEnvironmentVariableReader.EmptyInstance, AsyncServiceProvider.GlobalProvider, _experimentationService);

            AppContext.TryGetSwitch(PowerShellScriptExecutionFeature.SwitchName, out bool previousValue);
            try
            {
                AppContext.SetSwitch(PowerShellScriptExecutionFeature.SwitchName, !flagEnabled);

                await service.InitializeAsync();

                AppContext.TryGetSwitch(PowerShellScriptExecutionFeature.SwitchName, out bool enabled).Should().BeTrue();
                enabled.Should().Be(flagEnabled);
            }
            finally
            {
                AppContext.SetSwitch(PowerShellScriptExecutionFeature.SwitchName, previousValue);
            }
        }

        [Fact]
        public async Task InitializeAsync_ConcurrentCallers_WaitForSharedInitializationAndPublishBothSwitchesOnce()
        {
            var experimentationService = new Mock<INuGetExperimentationService>(MockBehavior.Strict);
            experimentationService.Setup(x => x.IsExperimentEnabled(ExperimentationConstants.UseSystemTextJsonDeserialization))
                .Returns(true);
            var featureFlags = new Mock<IVsFeatureFlags>(MockBehavior.Strict);
            featureFlags.Setup(x => x.IsFeatureEnabled("NuGet.ControlPowerShellScriptExecution", false)).Returns(true);
            var featureFlagsAvailable = new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var serviceProvider = new Mock<IAsyncServiceProvider>(MockBehavior.Strict);
            serviceProvider.Setup(x => x.GetServiceAsync(typeof(SVsFeatureFlags))).Returns(featureFlagsAvailable.Task);
            var service = new NuGetFeatureFlagService(
                TestEnvironmentVariableReader.EmptyInstance, serviceProvider.Object, experimentationService.Object);

            string jsonSwitchName = ExperimentationConstants.UseSystemTextJsonDeserialization.FlightFlag;
            AppContext.TryGetSwitch(jsonSwitchName, out bool previousJsonValue);
            AppContext.TryGetSwitch(PowerShellScriptExecutionFeature.SwitchName, out bool previousValue);
            try
            {
                AppContext.SetSwitch(jsonSwitchName, false);
                AppContext.SetSwitch(PowerShellScriptExecutionFeature.SwitchName, false);

                Task firstCaller = service.InitializeAsync();
                Task secondCaller = service.InitializeAsync();

                firstCaller.IsCompleted.Should().BeFalse();
                secondCaller.IsCompleted.Should().BeFalse();
                AppContext.TryGetSwitch(PowerShellScriptExecutionFeature.SwitchName, out bool enabled);
                enabled.Should().BeFalse();

                featureFlagsAvailable.SetResult(featureFlags.Object);
                await Task.WhenAll(firstCaller, secondCaller);
                await service.InitializeAsync();

                AppContext.TryGetSwitch(PowerShellScriptExecutionFeature.SwitchName, out enabled).Should().BeTrue();
                enabled.Should().BeTrue();
                AppContext.TryGetSwitch(jsonSwitchName, out bool jsonEnabled).Should().BeTrue();
                jsonEnabled.Should().BeTrue();
                experimentationService.Verify(
                    x => x.IsExperimentEnabled(ExperimentationConstants.UseSystemTextJsonDeserialization), Times.Once);
                featureFlags.Verify(x => x.IsFeatureEnabled("NuGet.ControlPowerShellScriptExecution", false), Times.Once);
                serviceProvider.Verify(x => x.GetServiceAsync(typeof(SVsFeatureFlags)), Times.Once);
            }
            finally
            {
                featureFlagsAvailable.TrySetCanceled();
                AppContext.SetSwitch(jsonSwitchName, previousJsonValue);
                AppContext.SetSwitch(PowerShellScriptExecutionFeature.SwitchName, previousValue);
            }
        }

        [Fact]
        public void Constructor_WithNullWrapper_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new NuGetFeatureFlagService(null, AsyncServiceProvider.GlobalProvider, _experimentationService));
        }

        [Fact]
        public void Constructor_WithNullAsyncServiceProvider_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new NuGetFeatureFlagService(new TestEnvironmentVariableReader(new Dictionary<string, string>()), null, _experimentationService));
        }

        [Fact]
        public void Constructor_WithNullExperimentationService_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new NuGetFeatureFlagService(
                TestEnvironmentVariableReader.EmptyInstance, AsyncServiceProvider.GlobalProvider, null));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]

        public async Task IsFeatureEnabledAsync_WithoutFlag_ReturnsDefaultValueFromConstant(bool featureFlagDefault)
        {
            var featureFlagConstant = new NuGetFeatureFlagConstants("featureFlag", "featureEnvVar", defaultState: featureFlagDefault);
            var vsFeatureFlags = Mock.Of<IVsFeatureFlags>();

            Mock.Get(vsFeatureFlags)
                .Setup(x => x.IsFeatureEnabled(
                    It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(featureFlagDefault);

            _globalProvider.AddService(typeof(SVsFeatureFlags), vsFeatureFlags);
            var service = new NuGetFeatureFlagService(new EnvironmentVariableWrapper(), AsyncServiceProvider.GlobalProvider, _experimentationService);
            (await service.IsFeatureEnabledAsync(featureFlagConstant)).Should().Be(featureFlagDefault);
        }

        [Fact]
        public async Task IsFeatureEnabledAsync_WithEnabledFeatureFlagAndForcedEnabledEnvVar_ReturnsTrue()
        {
            var featureFlagConstant = new NuGetFeatureFlagConstants("featureFlag", "featureEnvVar", defaultState: false);
            var envVars = new Dictionary<string, string>()
            {
                { featureFlagConstant.EnvironmentVariable, "1" },
            };
            var envVarWrapper = new TestEnvironmentVariableReader(envVars);
            var vsFeatureFlags = Mock.Of<IVsFeatureFlags>();

            Mock.Get(vsFeatureFlags)
                .Setup(x => x.IsFeatureEnabled(
                    It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(true);

            _globalProvider.AddService(typeof(SVsFeatureFlags), vsFeatureFlags);
            var service = new NuGetFeatureFlagService(envVarWrapper, AsyncServiceProvider.GlobalProvider, _experimentationService);
            (await service.IsFeatureEnabledAsync(featureFlagConstant)).Should().Be(true);

        }

        [Theory]
        [InlineData("2")]
        [InlineData("randomValue")]
        public async Task IsFeatureEnabledAsync_WithEnvVarWithIncorrectValue_WithEnvironmentVariable__ReturnsFalse(string value)
        {
            var featureFlagConstant = new NuGetFeatureFlagConstants("featureFlag", "featureEnvVar", defaultState: false);
            var envVars = new Dictionary<string, string>()
            {
                { featureFlagConstant.EnvironmentVariable, value },
            };
            var envVarWrapper = new TestEnvironmentVariableReader(envVars);
            var vsFeatureFlags = Mock.Of<IVsFeatureFlags>();

            Mock.Get(vsFeatureFlags)
                .Setup(x => x.IsFeatureEnabled(
                    It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(false);

            _globalProvider.AddService(typeof(SVsFeatureFlags), vsFeatureFlags);
            var service = new NuGetFeatureFlagService(envVarWrapper, AsyncServiceProvider.GlobalProvider, _experimentationService);
            (await service.IsFeatureEnabledAsync(featureFlagConstant)).Should().Be(false);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(false, false)]
        public async Task IsFeatureEnabledAsync_WithEnvVarNotSetWithEnabledFeatureFromWithFeatureFlagService_ReturnsExpectedResult(bool isFeatureEnabled, bool expectedResult)
        {
            var featureFlagConstant = new NuGetFeatureFlagConstants("featureFlag", "featureEnvVar", defaultState: false);
            var envVarWrapper = new TestEnvironmentVariableReader(new Dictionary<string, string>());
            var vsFeatureFlags = Mock.Of<IVsFeatureFlags>();

            Mock.Get(vsFeatureFlags)
                .Setup(x => x.IsFeatureEnabled(
                    It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(isFeatureEnabled);

            _globalProvider.AddService(typeof(SVsFeatureFlags), vsFeatureFlags);
            var service = new NuGetFeatureFlagService(envVarWrapper, AsyncServiceProvider.GlobalProvider, _experimentationService);
            (await service.IsFeatureEnabledAsync(featureFlagConstant)).Should().Be(expectedResult);
        }

        [Fact]
        public async Task IsFeatureEnabledAsync_WithEnvVarEnabled_WithFeatureFlagServiceDisabled_ReturnsTrue()
        {
            var featureFlagConstant = new NuGetFeatureFlagConstants("featureFlag", "featureEnvVar", defaultState: false);
            var envVars = new Dictionary<string, string>()
            {
                { featureFlagConstant.EnvironmentVariable, "1" },
            };
            var envVarWrapper = new TestEnvironmentVariableReader(envVars);
            var vsFeatureFlags = Mock.Of<IVsFeatureFlags>();

            Mock.Get(vsFeatureFlags)
                .Setup(x => x.IsFeatureEnabled(
                    It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(false);

            _globalProvider.AddService(typeof(SVsFeatureFlags), vsFeatureFlags);
            var service = new NuGetFeatureFlagService(envVarWrapper, AsyncServiceProvider.GlobalProvider, _experimentationService);
            (await service.IsFeatureEnabledAsync(featureFlagConstant)).Should().Be(true);
        }

        [Fact]
        public async Task IsFeatureEnabledAsync_WithEnvVarDisabled_WithFeatureFlagServiceEnabled_ReturnsFalse()
        {
            var featureFlagConstant = new NuGetFeatureFlagConstants("featureFlag", "featureEnvVar", defaultState: false);
            var envVars = new Dictionary<string, string>()
            {
                { featureFlagConstant.EnvironmentVariable, "0" },
            };
            var envVarWrapper = new TestEnvironmentVariableReader(envVars);
            var vsFeatureFlags = Mock.Of<IVsFeatureFlags>();

            Mock.Get(vsFeatureFlags)
                .Setup(x => x.IsFeatureEnabled(
                    It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(true);

            _globalProvider.AddService(typeof(SVsFeatureFlags), vsFeatureFlags);
            var service = new NuGetFeatureFlagService(envVarWrapper, AsyncServiceProvider.GlobalProvider, _experimentationService);
            (await service.IsFeatureEnabledAsync(featureFlagConstant)).Should().Be(false);
        }

        [Fact]
        public async Task IsFeatureEnabledAsync_WithNullEnvironmentVariableForConstant_HandlesGracefully()
        {
            var featureFlagConstant = new NuGetFeatureFlagConstants("featureFlag", null, defaultState: false);
            var vsFeatureFlags = Mock.Of<IVsFeatureFlags>();

            _globalProvider.AddService(typeof(SVsFeatureFlags), vsFeatureFlags);
            var service = new NuGetFeatureFlagService(new EnvironmentVariableWrapper(), AsyncServiceProvider.GlobalProvider, _experimentationService);
            (await service.IsFeatureEnabledAsync(featureFlagConstant)).Should().Be(false);
        }

        [Fact]
        public async Task IsFeatureEnabledAsync_MultipleFeaturesOverriddenWithDifferentEnvVars_DoNotConflict()
        {
            var forcedOff = new NuGetFeatureFlagConstants("TestExp1", "TEST_EXP_1", defaultState: false);
            var forcedOn = new NuGetFeatureFlagConstants("TestExp2", "TEST_EXP_2", defaultState: false);
            var noOverride = new NuGetFeatureFlagConstants("TestExp3", "TEST_EXP_3", defaultState: false);
            var envVars = new Dictionary<string, string>()
            {
                { forcedOn.EnvironmentVariable, "1" },
                { forcedOff.EnvironmentVariable, "0" },
            };
            var envVarWrapper = new TestEnvironmentVariableReader(envVars);
            var vsFeatureFlags = Mock.Of<IVsFeatureFlags>();

            Mock.Get(vsFeatureFlags)
                .Setup(x => x.IsFeatureEnabled(
                    It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(true);

            _globalProvider.AddService(typeof(SVsFeatureFlags), vsFeatureFlags);

            var service = new NuGetFeatureFlagService(envVarWrapper, AsyncServiceProvider.GlobalProvider, _experimentationService);

            (await service.IsFeatureEnabledAsync(forcedOff)).Should().BeFalse();
            (await service.IsFeatureEnabledAsync(forcedOn)).Should().BeTrue();
            (await service.IsFeatureEnabledAsync(noOverride)).Should().BeTrue();
        }
    }
}
