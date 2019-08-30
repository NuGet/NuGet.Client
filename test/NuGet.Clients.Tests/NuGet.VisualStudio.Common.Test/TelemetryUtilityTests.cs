// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.VisualStudio.Telemetry;
using Xunit;

namespace NuGet.VisualStudio.Common.Test
{
    public class TelemetryUtilityTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void CreateFileAndForgetEventName_WhenTypeNameIsNullOrEmpty_Throws(string typeName)
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(() => TelemetryUtility.CreateFileAndForgetEventName(typeName, "memberName"));

            Assert.Equal("typeName", exception.ParamName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void CreateFileAndForgetEventName_WhenMemberNameIsNullOrEmpty_Throws(string memberName)
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(() => TelemetryUtility.CreateFileAndForgetEventName("typeName", memberName));

            Assert.Equal("memberName", exception.ParamName);
        }

        [Fact]
        public void CreateFileAndForgetEventName_WhenArgumentsAreValid_ReturnsString()
        {
            string actualResult = TelemetryUtility.CreateFileAndForgetEventName("a", "b");

            Assert.Equal("VS/NuGet/fileandforget/a/b", actualResult);
        }
    }
}
