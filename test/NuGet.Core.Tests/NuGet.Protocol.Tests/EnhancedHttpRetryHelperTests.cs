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
            Assert.Equal(EnhancedHttpRetryHelper.DefaultRetryCount, helper.RetryCountOrDefault);
            Assert.Equal(EnhancedHttpRetryHelper.DefaultDelayMilliseconds, helper.DelayInMillisecondsOrDefault);
            Assert.Equal(EnhancedHttpRetryHelper.DefaultRetry429, helper.Retry429OrDefault);
            Assert.Equal(EnhancedHttpRetryHelper.DefaultObserveRetryAfter, helper.ObserveRetryAfterOrDefault);
            Assert.Equal(TimeSpan.FromSeconds(EnhancedHttpRetryHelper.DefaultMaximumRetryAfterDelayInSeconds), helper.MaxRetryAfterDelayOrDefault);
            Assert.Null(helper.DelayInMilliseconds);
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
            Assert.Equal(EnhancedHttpRetryHelper.DefaultRetryCount, helper.RetryCountOrDefault);
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
            Assert.Equal(value, helper.RetryCountOrDefault);
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
            Assert.Equal(EnhancedHttpRetryHelper.DefaultDelayMilliseconds, helper.DelayInMillisecondsOrDefault);
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
            Assert.Equal(value, helper.DelayInMillisecondsOrDefault);
        }
    }
}
