using System;
using System.IO;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class NuGetHelpCommandTest
    {
        [Theory]
        [InlineData("config")]
        [InlineData("delete")]
        [InlineData("install")]
        [InlineData("list")]
        [InlineData("pack")]
        [InlineData("push")]
        [InlineData("restore")]
        [InlineData("setApiKey")]
        [InlineData("sources")]
        [InlineData("spec")]
        [InlineData("update")]
        [InlineData("init")]
        [InlineData("add")]
        public void HelpCommand_HelpMessage(string command)
        {
            // Arrange
            var nugetexe = Util.GetNuGetExePath();

            // Act
            var r = CommandRunner.Run(
                nugetexe,
                Directory.GetCurrentDirectory(),
                "help " + command,
                waitForExit: true);

            // Assert
            Assert.True(0 == r.Item1, r.Item2 + Environment.NewLine + r.Item3);
        }

        // Tests that -ConfigFile is not included in the help message
        [Fact]
        public void HelpCommand_SpecCommand()
        {
            // Arrange
            var nugetexe = Util.GetNuGetExePath();

            // Act
            var r = CommandRunner.Run(
                nugetexe,
                Directory.GetCurrentDirectory(),
                "help spec",
                waitForExit: true);

            // Assert
            Assert.Equal(0, r.Item1);
            Assert.DoesNotContain("-ConfigFile", r.Item2, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void HelpCommand_Failure_InvalidArguments()
        {
            Util.TestCommandInvalidArguments("help aCommand otherCommand");
        }
    }
}
