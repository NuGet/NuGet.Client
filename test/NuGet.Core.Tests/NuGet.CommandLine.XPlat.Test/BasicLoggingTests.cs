using Xunit;

namespace NuGet.CommandLine.XPlat.Test
{
    public class BasicLoggingTests
    {
        [Fact]
        public void BasicLogging_VerifyExceptionLogged()
        {
            // Arrange
            var log = new TestCommandOutputLogger();
            Program.Log = log;

            var args = new string[]
            {
                "--unknown",
            };

            // Act
            var exitCode = Program.Main(args);

            // Assert
            Assert.Equal(1, exitCode);
            Assert.Equal(3, log.Messages.Count);
            Assert.Equal(1, log.Errors);
            Assert.Equal(0, log.Warnings);
            Assert.Contains("--unknown", log.Messages.ToArray()[1]);  // error
            Assert.Contains("Program.cs", log.Messages.ToArray()[2]); // verbose stack trace
        }

        [Fact]
        public void BasicLogging_NoParams_ExitCode()
        {
            // Arrange
            var log = new TestCommandOutputLogger();
            Program.Log = log;

            var args = new string[]
            {
                //empty
            };

            // Act
            var exitCode = Program.Main(args);

            // Assert
            Assert.Equal(0, exitCode);
        }

        [Fact]
        public void BasicLogging_RestoreHelp_ExitCode()
        {
            // Arrange
            var log = new TestCommandOutputLogger();
            Program.Log = log;

            var args = new string[]
            {
                "restore",
                "--help",
            };

            // Act
            var exitCode = Program.Main(args);

            // Assert
            Assert.Equal(0, exitCode);
        }

        [Fact(Skip = "Not working on CLI")]
        public void BasicLogging_RestoreConfigFile_ExitCode()
        {
            // Arrange
            var log = new TestCommandOutputLogger();
            Program.Log = log;

            var args = new string[]
            {
                "restore",
                "--configfile",
                "MyNuGet.config",
            };

            // Act
            var exitCode = Program.Main(args);

            // Assert
            Assert.Equal(1, exitCode);
            Assert.Equal(1, log.Errors);
            Assert.Contains("MyNuGet.config", log.Messages.ToArray()[1]); // file does not exist
        }

        [Fact]
        public void BasicLogging_RestoreUnknownOption_ExitCode()
        {
            // Arrange
            var log = new TestCommandOutputLogger();
            Program.Log = log;

            var args = new string[]
            {
                "restore",
                "--unknown",
            };

            // Act
            var exitCode = Program.Main(args);

            // Assert
            Assert.Equal(1, exitCode);
            Assert.Equal(3, log.Messages.Count);
            Assert.Equal(1, log.Errors);
            Assert.Equal(0, log.Warnings);
            Assert.Contains("--unknown", log.Messages.ToArray()[1]);  // error
            Assert.Contains("Program.cs", log.Messages.ToArray()[2]); // verbose stack trace
        }
    }
}
