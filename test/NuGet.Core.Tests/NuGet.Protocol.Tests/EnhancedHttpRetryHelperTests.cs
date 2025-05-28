// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using Test.Utility;
using Xunit;

namespace NuGet.Protocol.Tests
{
    public class EnhancedHttpRetryHelperTests
    {
        [Fact]
        public void NoEnvironmentVariablesSet_UsesDefaultValues()
        {
            // Arrange
            TestEnvironmentVariableReader testEnvironmentVariableReader = new TestEnvironmentVariableReader(new Dictionary<string, string>());

            // Act
            EnhancedHttpRetryHelper helper = new(testEnvironmentVariableReader);

            // Assert
            Assert.Equal(helper.RetryCountOrDefault, EnhancedHttpRetryHelper.DefaultRetryCount);
            Assert.Equal(helper.DelayInMillisecondsOrDefault, EnhancedHttpRetryHelper.DefaultDelayMilliseconds);
            Assert.Equal(helper.Retry429OrDefault, EnhancedHttpRetryHelper.DefaultRetry429);
            Assert.Equal(helper.ObserveRetryAfterOrDefault, EnhancedHttpRetryHelper.DefaultObserveRetryAfter);
            Assert.Equal(helper.MaxRetryAfterDelayOrDefault, TimeSpan.FromSeconds(EnhancedHttpRetryHelper.DefaultMaximumRetryAfterDelayInSeconds));
            Assert.Equal(helper.DelayInMilliseconds, null);
        }

        [Theory]
        [InlineData("")]
        [InlineData("true")]
        [InlineData("something")]
        [InlineData("-5")]
        public void RetryCount_InvalidIntValue_UsesDefault(string value)
        {
            // Arrange
            var environmentReader = new TestEnvironmentVariableReader(new Dictionary<string, string>()
            {
                [EnhancedHttpRetryHelper.RetryCountEnvironmentVariableName] = value
            });

            // Act
            EnhancedHttpRetryHelper helper = new(environmentReader);

            // Assert
            Assert.Equal(helper.RetryCountOrDefault, EnhancedHttpRetryHelper.DefaultRetryCount);
        }

        [Theory]
        [InlineData(5)]
        [InlineData(10)]
        [InlineData(100)]
        public void RetryCount_ValidIntValue_UsesValue(int value)
        {
            // Arrange
            var environmentReader = new TestEnvironmentVariableReader(new Dictionary<string, string>()
            {
                [EnhancedHttpRetryHelper.RetryCountEnvironmentVariableName] = value.ToString().ToLowerInvariant()
            });

            // Act
            EnhancedHttpRetryHelper helper = new(environmentReader);

            // Assert
            Assert.Equal(helper.RetryCountOrDefault, value);
        }

        [Theory]
        [InlineData("")]
        [InlineData("true")]
        [InlineData("something")]
        [InlineData("-5")]
        public void DelayInMilliseconds_InvalidIntValue_UsesDefault(string value)
        {
            // Arrange
            var environmentReader = new TestEnvironmentVariableReader(new Dictionary<string, string>()
            {
                [EnhancedHttpRetryHelper.DelayInMillisecondsEnvironmentVariableName] = value
            });

            // Act
            EnhancedHttpRetryHelper helper = new(environmentReader);

            // Assert
            Assert.Equal(helper.DelayInMillisecondsOrDefault, EnhancedHttpRetryHelper.DefaultDelayMilliseconds);
        }

        [Theory]
        [InlineData(5)]
        [InlineData(10)]
        [InlineData(100)]
        public void DelayInMilliseconds_ValidIntValue_UsesValue(int value)
        {
            // Arrange
            var environmentReader = new TestEnvironmentVariableReader(new Dictionary<string, string>()
            {
                [EnhancedHttpRetryHelper.DelayInMillisecondsEnvironmentVariableName] = value.ToString().ToLowerInvariant()
            });

            // Act
            EnhancedHttpRetryHelper helper = new(environmentReader);

            // Assert
            Assert.Equal(helper.DelayInMillisecondsOrDefault, value);
        }
    }
}
