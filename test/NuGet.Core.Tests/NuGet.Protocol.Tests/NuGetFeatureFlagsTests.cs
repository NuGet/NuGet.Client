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
        public void NSJDeserializationFeatureSwitch_Default_ReturnsTrue()
        {
            Assert.True(NuGetFeatureFlags.NSJDeserializationFeatureSwitch);
        }

        [Fact]
        public void IsNSJDeserializationEnabledByEnvironment_WhenEnvVarNotSet_ReturnsTrue()
        {
            Assert.True(NuGetFeatureFlags.IsNSJDeserializationEnabledByEnvironment(TestEnvironmentVariableReader.EmptyInstance));
        }

        [Theory]
        [InlineData("false")]
        [InlineData("False")]
        [InlineData("FALSE")]
        public void IsNSJDeserializationEnabledByEnvironment_WhenEnvVarSetToFalse_ReturnsFalse(string value)
        {
            var env = new TestEnvironmentVariableReader(
                new Dictionary<string, string> { [NuGetFeatureFlags.UsesNSJDeserializationEnvVar] = value });

            Assert.False(NuGetFeatureFlags.IsNSJDeserializationEnabledByEnvironment(env));
        }

        [Theory]
        [InlineData("true")]
        [InlineData("True")]
        [InlineData("0")]
        [InlineData("1")]
        [InlineData("anything")]
        public void IsNSJDeserializationEnabledByEnvironment_WhenEnvVarSetToTrueOrUnrecognized_ReturnsTrue(string value)
        {
            var env = new TestEnvironmentVariableReader(
                new Dictionary<string, string> { [NuGetFeatureFlags.UsesNSJDeserializationEnvVar] = value });

            Assert.True(NuGetFeatureFlags.IsNSJDeserializationEnabledByEnvironment(env));
        }
    }
}
