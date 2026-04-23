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
        public void UseLegacyJsonDeserializationFeatureSwitch_Default_ReturnsFalse()
        {
            Assert.False(NuGetFeatureFlags.UseLegacyJsonDeserializationFeatureSwitch);
        }

        [Fact]
        public void IsLegacyJsonDeserializationEnabledByEnvironment_WhenEnvVarNotSet_ReturnsFalse()
        {
            Assert.False(NuGetFeatureFlags.IsLegacyJsonDeserializationEnabledByEnvironment(TestEnvironmentVariableReader.EmptyInstance));
        }

        [Theory]
        [InlineData("true")]
        [InlineData("True")]
        [InlineData("TRUE")]
        public void IsLegacyJsonDeserializationEnabledByEnvironment_WhenEnvVarSetToTrue_ReturnsTrue(string value)
        {
            var env = new TestEnvironmentVariableReader(
                new Dictionary<string, string> { [NuGetFeatureFlags.UseLegacyJsonDeserializationEnvVar] = value });

            Assert.True(NuGetFeatureFlags.IsLegacyJsonDeserializationEnabledByEnvironment(env));
        }

        [Theory]
        [InlineData("false")]
        [InlineData("0")]
        [InlineData("1")]
        [InlineData("anything")]
        public void IsLegacyJsonDeserializationEnabledByEnvironment_WhenEnvVarSetToFalseOrUnrecognized_ReturnsFalse(string value)
        {
            var env = new TestEnvironmentVariableReader(
                new Dictionary<string, string> { [NuGetFeatureFlags.UseLegacyJsonDeserializationEnvVar] = value });

            Assert.False(NuGetFeatureFlags.IsLegacyJsonDeserializationEnabledByEnvironment(env));
        }
    }
}
