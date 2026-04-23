// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Shared;
using Test.Utility;
using Xunit;

namespace NuGet.Protocol.Tests
{
    public class NuGetFeatureFlagsTests
    {
        [Fact]
        public void UseNSJDeserializationFeatureSwitch_Default_ReturnsFalse()
        {
            Assert.False(NuGetFeatureFlags.UseNSJDeserializationFeatureSwitch);
        }

        [Fact]
        public void IsNSJDeserializationEnabledByEnvironment_WhenEnvVarNotSet_ReturnsFalse()
        {
            Assert.False(NuGetFeatureFlags.IsNSJDeserializationEnabledByEnvironment(TestEnvironmentVariableReader.EmptyInstance));
        }

        [Theory]
        [InlineData("true")]
        [InlineData("True")]
        [InlineData("TRUE")]
        public void IsNSJDeserializationEnabledByEnvironment_WhenEnvVarSetToTrue_ReturnsTrue(string value)
        {
            var env = new TestEnvironmentVariableReader(
                new Dictionary<string, string> { [NuGetFeatureFlags.UseNSJDeserializationEnvVar] = value });

            Assert.True(NuGetFeatureFlags.IsNSJDeserializationEnabledByEnvironment(env));
        }

        [Theory]
        [InlineData("false")]
        [InlineData("0")]
        [InlineData("1")]
        [InlineData("anything")]
        public void IsNSJDeserializationEnabledByEnvironment_WhenEnvVarSetToFalseOrUnrecognized_ReturnsFalse(string value)
        {
            var env = new TestEnvironmentVariableReader(
                new Dictionary<string, string> { [NuGetFeatureFlags.UseNSJDeserializationEnvVar] = value });

            Assert.False(NuGetFeatureFlags.IsNSJDeserializationEnabledByEnvironment(env));
        }
    }
}
