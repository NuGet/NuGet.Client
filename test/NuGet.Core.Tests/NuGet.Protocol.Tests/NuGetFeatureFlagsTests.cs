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
        public void UseSystemTextJsonDeserializationFeatureSwitch_Default_ReturnsFalse()
        {
            Assert.False(NuGetFeatureFlags.UseSystemTextJsonDeserializationFeatureSwitch);
        }

        [Fact]
        public void IsSystemTextJsonDeserializationDisabledByEnvironment_WhenEnvVarNotSet_ReturnsFalse()
        {
            Assert.False(NuGetFeatureFlags.IsSystemTextJsonDeserializationDisabledByEnvironment(TestEnvironmentVariableReader.EmptyInstance));
        }

        [Theory]
        [InlineData("true")]
        [InlineData("True")]
        [InlineData("TRUE")]
        [InlineData("0")]
        [InlineData("1")]
        [InlineData("anything")]
        public void IsSystemTextJsonDeserializationDisabledByEnvironment_WhenEnvVarIsNotFalse_ReturnsFalse(string value)
        {
            var env = new TestEnvironmentVariableReader(
                new Dictionary<string, string> { [NuGetFeatureFlags.UseSystemTextJsonDeserializationEnvVar] = value });

            Assert.False(NuGetFeatureFlags.IsSystemTextJsonDeserializationDisabledByEnvironment(env));
        }

        [Theory]
        [InlineData("false")]
        [InlineData("False")]
        [InlineData("FALSE")]
        public void IsSystemTextJsonDeserializationDisabledByEnvironment_WhenEnvVarSetToFalse_ReturnsTrue(string value)
        {
            var env = new TestEnvironmentVariableReader(
                new Dictionary<string, string> { [NuGetFeatureFlags.UseSystemTextJsonDeserializationEnvVar] = value });

            Assert.True(NuGetFeatureFlags.IsSystemTextJsonDeserializationDisabledByEnvironment(env));
        }

        [Theory]
        [InlineData("true", false)]
        [InlineData("false", true)]
        public void IsSystemTextJsonDeserializationDisabledByConfiguration_ReturnsExpectedResult(string value, bool expected)
        {
            var env = new TestEnvironmentVariableReader(
                new Dictionary<string, string> { [NuGetFeatureFlags.UseSystemTextJsonDeserializationEnvVar] = value });

            Assert.Equal(expected, NuGetFeatureFlags.IsSystemTextJsonDeserializationDisabledByConfiguration(env));
        }
    }
}
