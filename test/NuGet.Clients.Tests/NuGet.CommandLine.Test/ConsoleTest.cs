using System;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class ConsoleTest
    {
        [Fact]
        public void TestConsoleWindowWidthNotZero()
        {
            Assert.NotEqual(0, new Console().WindowWidth);
        }
    }
}