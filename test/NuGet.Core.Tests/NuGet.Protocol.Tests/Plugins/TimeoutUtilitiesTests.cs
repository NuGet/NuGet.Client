// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit;

namespace NuGet.Protocol.Plugins.Tests
{
    public class TimeoutUtilitiesTests
    {
        [Fact]
        public void IsValid_ReturnsFalseForTimeSpanZero()
        {
            Assert.False(TimeoutUtilities.IsValid(TimeSpan.Zero));
        }

        [Fact]
        public void IsValid_ReturnsFalseForNegativeTimeSpan()
        {
            Assert.False(TimeoutUtilities.IsValid(TimeSpan.FromSeconds(-1)));
        }

        [Fact]
        public void IsValid_ReturnsFalseForTooLargeTimeSpan()
        {
            var milliseconds = int.MaxValue + 1L;

            Assert.False(TimeoutUtilities.IsValid(TimeSpan.FromMilliseconds(milliseconds)));
        }

        [Fact]
        public void IsValid_ReturnsTrueForMinimumAcceptableTimeSpan()
        {
            Assert.True(TimeoutUtilities.IsValid(ProtocolConstants.MinTimeout));
        }

        [Fact]
        public void IsValid_ReturnsTrueForMaximumAcceptableTimeSpan()
        {
            Assert.True(TimeoutUtilities.IsValid(ProtocolConstants.MaxTimeout));
        }
    }
}