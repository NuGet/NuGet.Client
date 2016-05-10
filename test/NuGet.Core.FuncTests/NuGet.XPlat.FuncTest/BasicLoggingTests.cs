using NuGet.CommandLine.XPlat;
using Xunit;

namespace NuGet.XPlat.FuncTest
{
    public class BasicLoggingTests
    {
        [Fact]
        public void BasicLogging_VerifyExceptionLogged()
        {
            // Arrange
            var log = new TestCommandOutputLogger();

            var args = new string[]
            {
                "--unknown",
            };

            // Act
            var exitCode = Program.MainInternal(args, log);

            // Assert
            Assert.Equal(1, exitCode);
            Assert.Equal(3, log.Messages.Count);
            Assert.Equal(1, log.Errors);
            Assert.Equal(0, log.Warnings);
            Assert.Contains("--unknown", log.ShowErrors());  // error
            Assert.Contains("NuGet.CommandLine.XPlat.Program.", log.ShowMessages()); // verbose stack trace
        }

        [Fact]
        public void BasicLogging_NoParams_ExitCode()
        {
            // Arrange
            var log = new TestCommandOutputLogger();

            var args = new string[]
            {
                //empty
            };

            // Act
            var exitCode = Program.MainInternal(args, log);

            // Assert
            Assert.Equal(0, exitCode);
        }

        [Fact]
        public void BasicLogging_RestoreHelp_ExitCode()
        {
            // Arrange
            var log = new TestCommandOutputLogger();

            var args = new string[]
            {
                "restore",
                "--help",
            };

            // Act
            var exitCode = Program.MainInternal(args, log);

            // Assert
            Assert.Equal(0, exitCode);
        }

        public void BasicLogging_RestoreConfigFile_ExitCode()
        {
            // Arrange
            var log = new TestCommandOutputLogger();

            var args = new string[]
            {
                "restore",
                "--configfile",
                "MyNuGet.config",
            };

            // Act
            var exitCode = Program.MainInternal(args, log);

            // Assert
            Assert.Equal(1, exitCode);
            Assert.Equal(1, log.Errors);
            Assert.Contains("MyNuGet.config", log.ShowErrors()); // file does not exist
        }

        [Fact]
        public void BasicLogging_RestoreUnknownOption_ExitCode()
        {
            // Arrange
            var log = new TestCommandOutputLogger();

            var args = new string[]
            {
                "restore",
                "--unknown",
            };

            // Act
            var exitCode = Program.MainInternal(args, log);

            // Assert
            Assert.Equal(1, exitCode);
            Assert.Equal(3, log.Messages.Count);
            Assert.Equal(1, log.Errors);
            Assert.Equal(0, log.Warnings);
            Assert.Contains("--unknown", log.ShowErrors());  // error
            Assert.Contains("NuGet.CommandLine.XPlat.Program.", log.ShowMessages()); // verbose stack trace
        }
    }
}
