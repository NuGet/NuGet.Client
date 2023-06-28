// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Xplat.Tests
{
    internal static class CommandTestUtils
    {
        internal static void AssertBothCommandSuccessfulExecution(int statusCurrent, int statusNew, TestLogger loggerCurrent, TestLogger loggerNew)
        {
            Assert.Equal(0, statusCurrent);
            Assert.Equal(0, statusNew);
            Assert.False(loggerCurrent.Messages.IsEmpty);
            Assert.False(loggerNew.Messages.IsEmpty);
            Assert.Equal(loggerCurrent.Messages, loggerNew.Messages);
        }
    }
}
