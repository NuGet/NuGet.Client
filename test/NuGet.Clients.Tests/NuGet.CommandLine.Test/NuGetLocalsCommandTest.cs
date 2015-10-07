using System.IO;
using System.Runtime.InteropServices.ComTypes;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class NuGetLocalsCommandTest
    {
        [Theory]
        [InlineData("locals")]
        [InlineData("locals -?")]
        [InlineData("locals all -list extraArg")]
        public void LocalsCommand_Success_InvalidArguments_HelpMessage(string args)
        {
            // Arrange & Act
            var result = CommandRunner.Run(
                Util.GetNuGetExePath(),
                Directory.GetCurrentDirectory(),
                args,
                waitForExit: true);

            // Assert
            Util.VerifyResultSuccess(result, "usage: NuGet locals <all | http-cache | packages-cache | global-packages> [-clear | -list]");
        }

        [Theory]
        [InlineData("locals unknownResource -list")]
        [InlineData("locals unknownResource -clear")]
        public void LocalsCommand_Success_InvalidLocalResourceName_HelpMessage(string args)
        {
            // Arrange & Act
            var result = CommandRunner.Run(
                Util.GetNuGetExePath(),
                Directory.GetCurrentDirectory(),
                args,
                waitForExit: true);

            // Assert
            Util.VerifyResultFailure(result, "An invalid local resource name was provided. Please provide one of the following values: http-cache, packages-cache, global-packages, all.");
        }

        [Theory]
        [InlineData("locals -list")]
        [InlineData("locals -clear")]
        public void LocalsCommand_Success_NoLocalResourceName_HelpMessage(string args)
        {
            // Arrange & Act
            var result = CommandRunner.Run(
                Util.GetNuGetExePath(),
                Directory.GetCurrentDirectory(),
                args,
                waitForExit: true);

            // Assert
            Util.VerifyResultSuccess(result, "usage: NuGet locals <all | http-cache | packages-cache | global-packages> [-clear | -list]");
        }

        [Theory]
        [InlineData("locals -list -clear")]
        [InlineData("locals all -clear -list")]
        public void LocalsCommand_Success_BothListAndClearOptions_HelpMessage(string args)
        {
            // Arrange & Act
            var result = CommandRunner.Run(
                Util.GetNuGetExePath(),
                Directory.GetCurrentDirectory(),
                args,
                waitForExit: true);

            // Assert
            Util.VerifyResultSuccess(result, "usage: NuGet locals <all | http-cache | packages-cache | global-packages> [-clear | -list]");
        }

        [Theory]
        [InlineData("http-cache")]
        [InlineData("packages-cache")]
        [InlineData("global-packages")]
        public void LocalsCommand_Success_ValidLocalResource_ListMessage(string args)
        {
            // Arrange & Act
            var result = CommandRunner.Run(
                Util.GetNuGetExePath(),
                Directory.GetCurrentDirectory(),
                $"locals {args} -list",
                waitForExit: true);

            // Assert
            Util.VerifyResultSuccess(result, $"{args}: ");
        }
    }
}