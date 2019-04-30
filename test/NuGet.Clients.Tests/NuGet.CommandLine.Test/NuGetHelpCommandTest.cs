using System;
using System.Collections.Generic;
using System.IO;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class NuGetHelpCommandTest
    {
        [Theory]
        [InlineData("add")]
        [InlineData("config")]
        [InlineData("delete")]
        [InlineData("help")]
        [InlineData("init")]
        [InlineData("install")]
        [InlineData("list")]
        [InlineData("locals")]
        [InlineData("pack")]
        [InlineData("push")]
        [InlineData("restore")]
        [InlineData("setApiKey")]
        [InlineData("sign")]
        [InlineData("sources")]
        [InlineData("spec")]
        [InlineData("trusted-signers")]
        [InlineData("update")]
        [InlineData("verify")]
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

        [Fact]
        public void HelpCommand_All()
        {
            // Arrange
            var nugetexe = Util.GetNuGetExePath();

            // Act
            var r = CommandRunner.Run(
                nugetexe,
                Directory.GetCurrentDirectory(),
                "help -all",
                waitForExit: true);

            // Assert
            Assert.True(r.Item1 == 0, r.AllOutput);
        }

        [Fact]
        public void HelpCommand_All_Markdown()
        {
            // Arrange
            var nugetexe = Util.GetNuGetExePath();

            // Act
            var r = CommandRunner.Run(
                nugetexe,
                Directory.GetCurrentDirectory(),
                "help -all -markdown",
                waitForExit: true,
                environmentVariables: new Dictionary<string, string>
                {
                    { "NUGET_SHOW_STACK", "true" }
                });

            // Assert
            Assert.True(r.Item1 == 0, r.AllOutput);
        }
    }
}
