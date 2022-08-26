// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.VisualStudio.Experimentation;
using Moq;
using NuGet.Common;
using Test.Utility;
using Test.Utility.VisualStudio;
using Xunit;

namespace NuGet.VisualStudio.Common.Test
{
    [Collection(nameof(TestJoinableTaskFactoryCollection))]
    public class NuGetExperimentationServiceTests
    {
        private readonly Lazy<IOutputConsoleProvider> _outputConsoleProvider;
        private IList<string> OutputMessages => ((TestOutputConsoleProvider)_outputConsoleProvider.Value).TestOutputConsole.Messages;

        public NuGetExperimentationServiceTests()
        {
            _outputConsoleProvider = new Lazy<IOutputConsoleProvider>(() => new TestOutputConsoleProvider());
        }

        [Fact]
        public void Constructor_WithNullEnvironmentVariableReader_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new NuGetExperimentationService(environmentVariableReader: null, Mock.Of<IExperimentationService>(), Mock.Of<Lazy<IOutputConsoleProvider>>()));
        }

        [Fact]
        public void Constructor_WithNullExperimentalService_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new NuGetExperimentationService(Mock.Of< IEnvironmentVariableReader>(), experimentationService: null, _outputConsoleProvider));
        }

        [Fact]
        public void Constructor_WithNullOutputConsoleProvider_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new NuGetExperimentationService(Mock.Of<IEnvironmentVariableReader>(), Mock.Of<IExperimentationService>(), outputConsoleProvider: null));
        }

        [Fact]
        public void IsEnabled_WithoutEnabledFlight_ReturnsFalse()
        {
            var service = new NuGetExperimentationService(Mock.Of<IEnvironmentVariableReader>(), NuGetExperimentationServiceUtility.GetMock(), _outputConsoleProvider);
            var constant = ExperimentationConstants.PackageManagerBackgroundColor;
            service.IsExperimentEnabled(constant).Should().BeFalse();
            OutputMessages.Should().NotContainMatch($"*{constant.FlightFlag}*{constant.FlightEnvironmentVariable}*");
        }

        [Fact]
        public void IsEnabled_WithEnabledFlightAndForcedEnabledEnvVar_ReturnsTrue()
        {
            var constant = ExperimentationConstants.PackageManagerBackgroundColor;
            var envVars = new Dictionary<string, string>()
            {
                { constant.FlightEnvironmentVariable, "1" },
            };
            var envVarWrapper = new TestEnvironmentVariableReader(envVars);
            var service = new NuGetExperimentationService(envVarWrapper, NuGetExperimentationServiceUtility.GetMock(), _outputConsoleProvider);

            service.IsExperimentEnabled(ExperimentationConstants.PackageManagerBackgroundColor).Should().BeTrue();
            OutputMessages.Should().ContainMatch($"*{constant.FlightFlag}*{constant.FlightEnvironmentVariable}*1*");
        }

        [Theory]
        [InlineData("2")]
        [InlineData("randomValue")]
        public void IsEnabled_WithEnvVarWithIncorrectValue_WithEnvironmentVariable__ReturnsFalse(string value)
        {
            var constant = ExperimentationConstants.PackageManagerBackgroundColor;
            var envVars = new Dictionary<string, string>()
            {
                { constant.FlightEnvironmentVariable, value },
            };
            var envVarWrapper = new TestEnvironmentVariableReader(envVars);
            var service = new NuGetExperimentationService(envVarWrapper, NuGetExperimentationServiceUtility.GetMock(), _outputConsoleProvider);

            service.IsExperimentEnabled(ExperimentationConstants.PackageManagerBackgroundColor).Should().BeFalse();
            OutputMessages.Should().NotContainMatch($"*{constant.FlightFlag}*{constant.FlightEnvironmentVariable}*");
        }

        [Fact]
        public void IsEnabled_WithEnabledFlight_WithExperimentalService_ReturnsTrue()
        {
            var constant = ExperimentationConstants.PackageManagerBackgroundColor;
            var flightsEnabled = new Dictionary<string, bool>()
            {
                { constant.FlightFlag, true },
            };
            var service = new NuGetExperimentationService(Mock.Of<IEnvironmentVariableReader>(), NuGetExperimentationServiceUtility.GetMock(flightsEnabled), _outputConsoleProvider);

            service.IsExperimentEnabled(ExperimentationConstants.PackageManagerBackgroundColor).Should().BeTrue();
            OutputMessages.Should().NotContainMatch($"*{constant.FlightFlag}*{constant.FlightEnvironmentVariable}*");
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(false, false)]
        public void IsEnabled_WithEnvVarNotSetAndExperimentalService_ReturnsExpectedResult(bool isFlightEnabled, bool expectedResult)
        {
            var constant = ExperimentationConstants.PackageManagerBackgroundColor;
            var flightsEnabled = new Dictionary<string, bool>()
            {
                { constant.FlightFlag, isFlightEnabled },
            };
            var service = new NuGetExperimentationService(Mock.Of<IEnvironmentVariableReader>(), NuGetExperimentationServiceUtility.GetMock(flightsEnabled), _outputConsoleProvider);

            service.IsExperimentEnabled(ExperimentationConstants.PackageManagerBackgroundColor).Should().Be(expectedResult);
            OutputMessages.Should().NotContainMatch($"*{constant.FlightFlag}*{constant.FlightEnvironmentVariable}*");
        }

        [Fact]
        public void IsEnabled_WithEnvVarEnabled_WithExperimentalServiceDisabled_ReturnsTrue()
        {
            var constant = ExperimentationConstants.PackageManagerBackgroundColor;
            var flightsEnabled = new Dictionary<string, bool>()
            {
                { constant.FlightFlag, false },
            };

            var envVars = new Dictionary<string, string>()
            {
                { constant.FlightEnvironmentVariable, "1" },
            };
            var envVarWrapper = new TestEnvironmentVariableReader(envVars);

            var service = new NuGetExperimentationService(envVarWrapper, NuGetExperimentationServiceUtility.GetMock(flightsEnabled), _outputConsoleProvider);

            service.IsExperimentEnabled(ExperimentationConstants.PackageManagerBackgroundColor).Should().BeTrue();
            OutputMessages.Should().ContainMatch($"*{constant.FlightFlag}*{constant.FlightEnvironmentVariable}*1*");
        }

        [Fact]
        public void IsEnabled_WithEnvVarDisabled_WithExperimentalServiceEnabled_ReturnsFalse()
        {
            var constant = ExperimentationConstants.PackageManagerBackgroundColor;
            var flightsEnabled = new Dictionary<string, bool>()
            {
                { constant.FlightFlag, true },
            };

            var envVars = new Dictionary<string, string>()
            {
                { constant.FlightEnvironmentVariable, "0" },
            };
            var envVarWrapper = new TestEnvironmentVariableReader(envVars);

            var service = new NuGetExperimentationService(envVarWrapper, NuGetExperimentationServiceUtility.GetMock(flightsEnabled), _outputConsoleProvider);

            service.IsExperimentEnabled(ExperimentationConstants.PackageManagerBackgroundColor).Should().BeFalse();
            OutputMessages.Should().ContainMatch($"*{constant.FlightFlag}*{constant.FlightEnvironmentVariable}*0*");
        }

        [Fact]
        public void IsEnabled_WithNullEnvironmentVariableForConstant_HandlesGracefully()
        {
            var service = new NuGetExperimentationService(Mock.Of<IEnvironmentVariableReader>(), NuGetExperimentationServiceUtility.GetMock(), _outputConsoleProvider);
            var constant = new ExperimentationConstants("flag", null);
            service.IsExperimentEnabled(constant).Should().BeFalse();
            OutputMessages.Should().NotContainMatch($"*{constant.FlightFlag}*{constant.FlightEnvironmentVariable}*");
        }

        [Fact]
        public void IsEnabled_MultipleExperimentsOverriddenWithDifferentEnvVars_DoNotConflict()
        {
            var forcedOffExperiment = new ExperimentationConstants("TestExp1", "TEST_EXP_1");
            var forcedOnExperiment = new ExperimentationConstants("TestExp2", "TEST_EXP_2");
            var noOverrideExperiment = new ExperimentationConstants("TestExp3", "TEST_EXP_3");
            var flightsEnabled = new Dictionary<string, bool>()
            {
                { forcedOffExperiment.FlightFlag, true },
                { forcedOnExperiment.FlightFlag, true },
                { noOverrideExperiment.FlightFlag, true },
            };
            var envVars = new Dictionary<string, string>()
            {
                { forcedOnExperiment.FlightEnvironmentVariable, "1" },
                { forcedOffExperiment.FlightEnvironmentVariable, "0" },
            };
            var envVarWrapper = new TestEnvironmentVariableReader(envVars);
            var service = new NuGetExperimentationService(envVarWrapper, NuGetExperimentationServiceUtility.GetMock(flightsEnabled), _outputConsoleProvider);

            service.IsExperimentEnabled(forcedOffExperiment).Should().BeFalse();
            service.IsExperimentEnabled(forcedOnExperiment).Should().BeTrue();
            service.IsExperimentEnabled(noOverrideExperiment).Should().BeTrue();
            OutputMessages.Should().ContainMatch($"*{forcedOffExperiment.FlightFlag}*{forcedOffExperiment.FlightEnvironmentVariable}*0*");
            OutputMessages.Should().ContainMatch($"*{forcedOnExperiment.FlightFlag}*{forcedOnExperiment.FlightEnvironmentVariable}*1*");
            OutputMessages.Should().NotContainMatch($"*{noOverrideExperiment.FlightFlag}*{noOverrideExperiment.FlightEnvironmentVariable}*");
        }
    }
}
