// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using NuGet.Configuration;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Xplat.Tests
{
    internal static class CommandTestUtils
    {
        internal static void AssertEqualCommandOutput(int statusCurrent, int statusNew, TestLogger loggerCurrent, TestLogger loggerNew)
        {
            Assert.Equal(0, statusCurrent);
            Assert.Equal(0, statusNew);
            Assert.False(loggerCurrent.Messages.IsEmpty);
            Assert.False(loggerNew.Messages.IsEmpty);
            Assert.Equal(loggerCurrent.Messages, loggerNew.Messages);
        }
    }
}
