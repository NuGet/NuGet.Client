using NuGet.CommandLine.XPlat;
using Xunit;

namespace NuGet.XPlat.FuncTest
{
    public class BasicLoggingTests
    {
        [Fact]
        public void BasicLogging_VersionHeading()
        {
            // Arrange
            var log = new TestCommandOutputLogger(observeLogLevel: true);

            var args = new string[]
            {
                "--verbosity",
                "verbose"
            };

            // Act
            var exitCode = Program.MainInternal(args, log);

            // Assert
            Assert.Equal(0, exitCode);
            Assert.Equal(1, log.VerboseMessages.Count);
            Assert.Equal(1, log.Messages.Count);
            Assert.Contains("NuGet Command Line Version:", log.ShowMessages());
        }

        [Fact]
        public void BasicLogging_RestoreVerbosityCanBeMoreVerboseThanGlobal()
        {
            // Arrange
            var log = new TestCommandOutputLogger(observeLogLevel: true);

            var args = new string[]
            {
                "--verbosity", "error", // Set the verbosity at a global level.
                "restore",
                "--configfile", "MyNuGet.config", // Cause a failure since we don't want a real restore to happen.
                "--disable-parallel", // Generate a verbose level log.
                "--verbosity", "verbose" // Set the verbosity at the command level.
            };

            // Act
            Program.MainInternal(args, log);

            // Assert
            Assert.Contains("Running non-parallel restore.", log.ShowMessages());
        }

        [Fact]
        public void BasicLogging_RestoreVerbosityCanBeLessVerboseThanGlobal()
        {
            // Arrange
            var log = new TestCommandOutputLogger(observeLogLevel: true);

            var args = new string[]
            {
                "--verbosity", "verbose", // Set the verbosity at a global level.
                "restore",
                "--configfile", "MyNuGet.config", // Cause a failure since we don't want a real restore to happen.
                "--disable-parallel", // Generate a verbose level log.
                "--verbosity", "error" // Set the verbosity at the command level.
            };

            // Act
            Program.MainInternal(args, log);

            // Assert
            Assert.DoesNotContain("Running non-parallel restore.", log.ShowMessages());
        }

        [Fact]
        public void BasicLogging_RestoreVerbosityDefaultsToGlobalVerbosity()
        {
            // Arrange
            var log = new TestCommandOutputLogger(observeLogLevel: true);

            var args = new string[]
            {
                "--verbosity", "verbose", // Set the verbosity at a global level.
                "restore",
                "--configfile", "MyNuGet.config", // Cause a failure since we don't want a real restore to happen.
                "--disable-parallel", // Generate a verbose level log.
            };

            // Act
            Program.MainInternal(args, log);

            // Assert
            Assert.Contains("Running non-parallel restore.", log.ShowMessages());
        }

        [Fact]
        public void BasicLogging_VerifyExceptionLoggedWhenVerbose()
        {
            // Arrange
            var log = new TestCommandOutputLogger(observeLogLevel: true);

            var args = new string[]
            {
                "--verbosity", "verbose",
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
        public void BasicLogging_VerifyExceptionNotLoggedLessThanVerbose()
        {
            // Arrange
            var log = new TestCommandOutputLogger(observeLogLevel: true);

            var args = new string[]
            {
                "--verbosity", "info",
                "--unknown",
            };

            // Act
            var exitCode = Program.MainInternal(args, log);

            // Assert
            Assert.Equal(1, exitCode);
            Assert.Equal(1, log.Messages.Count);
            Assert.Equal(1, log.Errors);
            Assert.Equal(0, log.Warnings);
            Assert.Contains("--unknown", log.ShowErrors());  // error
            Assert.DoesNotContain("NuGet.CommandLine.XPlat.Program.", log.ShowMessages()); // verbose stack trace
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

        [Fact]
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
