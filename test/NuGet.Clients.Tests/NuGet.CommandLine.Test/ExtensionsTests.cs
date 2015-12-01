using System;
using Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class ExtensionsTests
    {
        [Fact]
        public void TestExtensionsFromProgramDirLoaded()
        {
            var nugetexe = Util.GetNuGetExePath();
            var randomTestFolder = TestFilesystemUtility.CreateRandomTestFolder();

            var result = CommandRunner.Run(
                nugetexe,
                randomTestFolder,
                "hello",
                true);

            Assert.Equal(result.Item2, "Hello!" + Environment.NewLine);
        }
    }
}