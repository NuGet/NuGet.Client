// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.Experimentation;
using NuGet.Common;
using Test.Utility;
using Xunit;

namespace NuGet.VisualStudio.Common.Test
{
    public class NuGetExperimentationServiceTests
    {
        [Fact]
        public void Constructor_WithNullWrapper_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new NuGetExperimentationService(null, new TestVisualStudioExperimentalService()));
        }

        [Fact]
        public void Constructor_WithNullExperimentalService_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new NuGetExperimentationService(new TestEnvironmentVariableReader(new Dictionary<string, string>()), null));
        }

        [Fact]
        public void IsEnabled_WithoutEnabledFlight_ReturnsFalse()
        {
            var service = new NuGetExperimentationService(new EnvironmentVariableWrapper(), new TestVisualStudioExperimentalService());
            service.IsExperimentEnabled(ExperimentationConstants.PackageManagerBackgroundColor).Should().BeFalse();
        }

        [Fact]
        public void IsEnabled_WithEnabledFlight_WithEnvironmentVariable_ReturnsTrue()
        {
            var constant = ExperimentationConstants.PackageManagerBackgroundColor;
            var envVars = new Dictionary<string, string>()
            {
                { constant.FlightEnvironmentVariable, "1" },
            };
            var envVarWrapper = new TestEnvironmentVariableReader(envVars);
            var service = new NuGetExperimentationService(envVarWrapper, new TestVisualStudioExperimentalService());

            service.IsExperimentEnabled(ExperimentationConstants.PackageManagerBackgroundColor).Should().BeTrue();
        }

        [Theory]
        [InlineData("0")]
        [InlineData("randomValue")]
        public void IsEnabled_WithEnvVarWithIncorrectValue_WithEnvironmentVariable__ReturnsFalse(string value)
        {
            var constant = ExperimentationConstants.PackageManagerBackgroundColor;
            var envVars = new Dictionary<string, string>()
            {
                { constant.FlightEnvironmentVariable, value },
            };
            var envVarWrapper = new TestEnvironmentVariableReader(envVars);
            var service = new NuGetExperimentationService(envVarWrapper, new TestVisualStudioExperimentalService());

            service.IsExperimentEnabled(ExperimentationConstants.PackageManagerBackgroundColor).Should().BeFalse();
        }

        [Fact]
        public void IsEnabled_WithEnabledFlight_WithExperimentalService_ReturnsTrue()
        {
            var constant = ExperimentationConstants.PackageManagerBackgroundColor;
            var flightsEnabled = new Dictionary<string, bool>()
            {
                { constant.FlightFlag, true },
            };
            var service = new NuGetExperimentationService(new TestEnvironmentVariableReader(new Dictionary<string, string>()), new TestVisualStudioExperimentalService(flightsEnabled));

            service.IsExperimentEnabled(ExperimentationConstants.PackageManagerBackgroundColor).Should().BeTrue();
        }

        [Fact]
        public void IsEnabled_WithEnvVarWithIncorrectValue_WithExperimentalService_ReturnsFalse()
        {
            var constant = ExperimentationConstants.PackageManagerBackgroundColor;
            var flightsEnabled = new Dictionary<string, bool>()
            {
                { constant.FlightFlag, false },
            };
            var service = new NuGetExperimentationService(new TestEnvironmentVariableReader(new Dictionary<string, string>()), new TestVisualStudioExperimentalService(flightsEnabled));

            service.IsExperimentEnabled(ExperimentationConstants.PackageManagerBackgroundColor).Should().BeFalse();
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

            var service = new NuGetExperimentationService(envVarWrapper, new TestVisualStudioExperimentalService(flightsEnabled));

            service.IsExperimentEnabled(ExperimentationConstants.PackageManagerBackgroundColor).Should().BeTrue();
        }

        [Fact]
        public void IsEnabled_WithEnvVarDisabled_WithExperimentalServiceEnabled_ReturnsTrue()
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

            var service = new NuGetExperimentationService(envVarWrapper, new TestVisualStudioExperimentalService(flightsEnabled));

            service.IsExperimentEnabled(ExperimentationConstants.PackageManagerBackgroundColor).Should().BeTrue();
        }

        [Fact]
        public void IsEnabled_WithNullEnvironmentVariableForConstant_HandlesGracefully()
        {
            var service = new NuGetExperimentationService(new EnvironmentVariableWrapper(), new TestVisualStudioExperimentalService());
            service.IsExperimentEnabled(new ExperimentationConstants("flag", null)).Should().BeFalse();
        }
    }

    public class TestVisualStudioExperimentalService : IExperimentationService
    {
        private readonly Dictionary<string, bool> _flights;

        public TestVisualStudioExperimentalService()
            : this(new Dictionary<string, bool>())
        {
        }

        public TestVisualStudioExperimentalService(Dictionary<string, bool> flights)
        {
            _flights = flights ?? throw new ArgumentNullException(nameof(flights));
        }

        public void Dispose()
        {
            // do nothing
        }

        public bool IsCachedFlightEnabled(string flight)
        {
            _flights.TryGetValue(flight, out bool result);
            return result;
        }

        public Task<bool> IsFlightEnabledAsync(string flight, CancellationToken token)
        {
            return Task.FromResult(IsCachedFlightEnabled(flight));
        }

        public void Start()
        {
            // do nothing.
        }
    }
}
