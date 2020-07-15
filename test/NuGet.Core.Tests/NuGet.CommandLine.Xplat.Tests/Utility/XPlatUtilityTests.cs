// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.CommandLine.XPlat;
using NuGet.Common;
using Xunit;

namespace NuGet.CommandLine.Xplat.Tests.Utility
{
    public class XPlatUtilityTests
    {
        [Theory]
        [InlineData("", LogLevel.Minimal)]
        [InlineData(null, LogLevel.Minimal)]
        [InlineData("  ", LogLevel.Minimal)]
        [InlineData("qu", LogLevel.Minimal)]
        [InlineData("quiet ", LogLevel.Minimal)]
        [InlineData(" q", LogLevel.Minimal)]
        [InlineData("m", LogLevel.Minimal)]
        [InlineData("M", LogLevel.Minimal)]
        [InlineData("mInImAl", LogLevel.Minimal)]
        [InlineData("MINIMAL", LogLevel.Minimal)]
        [InlineData("something-else-entirely", LogLevel.Minimal)]
        [InlineData("q", LogLevel.Warning)]
        [InlineData("quiet", LogLevel.Warning)]
        [InlineData("Q", LogLevel.Warning)]
        [InlineData("QUIET", LogLevel.Warning)]
        [InlineData("n", LogLevel.Information)]
        [InlineData("normal", LogLevel.Information)]
        [InlineData("d", LogLevel.Debug)]
        [InlineData("detailed", LogLevel.Debug)]
        [InlineData("diag", LogLevel.Debug)]
        [InlineData("diagnostic", LogLevel.Debug)]
        public void MSBuildVerbosityToNuGetLogLevel_HasProperMapping(string verbosity, LogLevel expected)
        {
            LogLevel actual = XPlatUtility.MSBuildVerbosityToNuGetLogLevel(verbosity);

            Assert.Equal(expected, actual);
        }
    }
}
