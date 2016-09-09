using System.IO;
using Moq;
using NuGet.CommandLine.Commands;
using NuGet.Commands;
using NuGet.Configuration;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class NuGetLocalsCommandTest
    {
        private const string LocalsHelpStringFragment =
            "usage: NuGet locals <all | http-cache | global-packages | temp> [-clear | -list]";

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
            Util.VerifyResultSuccess(result, LocalsHelpStringFragment);
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
            Util.VerifyResultFailure(result, "An invalid local resource name was provided. Please provide one of the following values: http-cache, temp, global-packages, all.");
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
            Util.VerifyResultSuccess(result, LocalsHelpStringFragment);
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
            Util.VerifyResultSuccess(result, LocalsHelpStringFragment);
        }

        [Theory]
        [InlineData("http-cache")]
        [InlineData("global-packages")]
        [InlineData("temp")]
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

        [Theory]
        [InlineData("all")]
        [InlineData("http-cache")]
        [InlineData("global-packages")]
        [InlineData("temp")]
        public async void LocalsCommand_ParsingValidation_WithNoConfigParam(string arg)
        {
            // Use a test directory to validate test key-value pairs within ISettings object passed to Runner
            using (var mockCurrentDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                Util.CreateDummyConfigFile(mockCurrentDirectory.Path);
                var mockLocalsCommandRunner = new Mock<ILocalsCommandRunner>();
                var mockConsole = new Mock<IConsole>();
                mockConsole.Setup(c => c.Verbosity).Returns(Verbosity.Detailed);
                LocalsCommand localsCommand = new LocalsCommand();
                localsCommand.LocalsCommandRunner = mockLocalsCommandRunner.Object;
                localsCommand.Clear = false;
                localsCommand.List = true;
                localsCommand.Console = mockConsole.Object;
                localsCommand.Arguments.Add(arg);
                localsCommand.CurrentDirectory = mockCurrentDirectory.Path;
                var defaultSettings = Configuration.Settings.LoadDefaultSettings(mockCurrentDirectory,
                                                                                 configFileName: null,
                                                                                 machineWideSettings: localsCommand.MachineWideSettings);

                // Act
                localsCommand.Execute();
                await localsCommand.ExecuteCommandAsync();

                // Assert
                mockLocalsCommandRunner.Verify(mock => mock.ExecuteCommand(It.Is<LocalsArgs>(l => l.List && !l.Clear &&
                                                                                             l.Arguments.Count == 1 && l.Arguments[0] == arg &&
                                                                                             l.LogError == mockConsole.Object.LogError &&
                                                                                             l.LogInformation == mockConsole.Object.LogInformation &&
                                                                                             SettingsUtility.GetConfigValue(l.Settings, "foo", false, false) == SettingsUtility.GetConfigValue(defaultSettings, "foo", false, false) &&
                                                                                             SettingsUtility.GetConfigValue(l.Settings, "kung foo", false, false) == SettingsUtility.GetConfigValue(defaultSettings, "kung foo", false, false))));
            }
        }

        [Theory]
        [InlineData("all")]
        [InlineData("http-cache")]
        [InlineData("global-packages")]
        [InlineData("temp")]
        public async void LocalsCommand_ParsingValidation_WithConfigParam(string arg)
        {
            // Use a test directory to validate test key-value pairs within ISettings object passed to Runner
            using (var mockCurrentDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var dummyConfigPath = Util.CreateDummyConfigFile(mockCurrentDirectory.Path);
                var mockLocalsCommandRunner = new Mock<ILocalsCommandRunner>();
                var mockConsole = new Mock<IConsole>();
                mockConsole.Setup(c => c.Verbosity).Returns(Verbosity.Detailed);
                LocalsCommand localsCommand = new LocalsCommand();
                localsCommand.LocalsCommandRunner = mockLocalsCommandRunner.Object;
                localsCommand.Clear = true;
                localsCommand.List = false;
                localsCommand.Console = mockConsole.Object;
                localsCommand.Arguments.Add(arg);
                localsCommand.ConfigFile = dummyConfigPath;
                localsCommand.CurrentDirectory = mockCurrentDirectory.Path;
                var directory = Path.GetDirectoryName(dummyConfigPath);
                var configFileName = Path.GetFileName(dummyConfigPath);
                var defaultSettings = Configuration.Settings.LoadDefaultSettings(directory,
                                                                                 configFileName,
                                                                                 localsCommand.MachineWideSettings);

                // Act
                localsCommand.Execute();
                await localsCommand.ExecuteCommandAsync();

                // Assert
                mockLocalsCommandRunner.Verify(mock => mock.ExecuteCommand(It.Is<LocalsArgs>(l => !l.List && l.Clear &&
                                                                                             l.Arguments.Count == 1 && l.Arguments[0] == arg &&
                                                                                             l.LogError == mockConsole.Object.LogError &&
                                                                                             l.LogInformation == mockConsole.Object.LogInformation &&
                                                                                             SettingsUtility.GetConfigValue(l.Settings, "foo", false, false) == SettingsUtility.GetConfigValue(defaultSettings, "foo", false, false) &&
                                                                                             SettingsUtility.GetConfigValue(l.Settings, "kung foo", false, false) == SettingsUtility.GetConfigValue(defaultSettings, "kung foo", false, false))));
            }
        }
    }
}