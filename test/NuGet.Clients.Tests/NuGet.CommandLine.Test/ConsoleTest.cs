using System;
using System.IO;
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


        [Theory]
        [InlineData(2, "abcd\nwxyz", "  abcdXXX  wxyzXXX", 10)]
        [InlineData(3, "abcde\n\nwcjb", "   abcdeXXXXXX   wcjbXXX", 10)]
        [InlineData(2, "abcde\r\nwcjb", "  abcdeXXX  wcjbXXX", 10)]
        [InlineData(2, "abcde\rwcjb", "  abcdeXXX  wcjbXXX", 10)]
        [InlineData(2, "abcdefghijk\nAB", "  abcdeXXX  fghijXXX  kXXX  ABXXX", 7)]
        public void TestPrintJustified(int indent, string input, string expected, int width)
        {
            var sw = new StringWriter();
            System.Console.SetOut(sw);

            var console = new Console();
            console.MockWindowWidth = width;
            console.PrintJustified(indent, input);

            Assert.Equal(expected.Replace("XXX", Environment.NewLine), sw.ToString());
        }

    }
}
