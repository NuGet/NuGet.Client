// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using Moq;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Protocol.Plugins.Tests
{
    public class PluginLoggerTests : LogMessageTests
    {
        [Fact]
        public void Constructor_WhenEnvironmentVariableReaderIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new PluginLogger(environmentVariableReader: null));

            Assert.Equal("environmentVariableReader", exception.ParamName);
        }

        [Fact]
        public void IsEnabled_WhenLoggingIsNotEnabled_ReturnsTrue()
        {
            var environmentVariableReader = CreateEnvironmentVariableReaderMock(isLoggingEnabled: false);

            using (var logger = new PluginLogger(environmentVariableReader.Object))
            {
                Assert.False(logger.IsEnabled);
            }

            environmentVariableReader.VerifyAll();
        }

        [Fact]
        public void IsEnabled_WhenLoggingIsEnabled_ReturnsTrue()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var environmentVariableReader = CreateEnvironmentVariableReaderMock(
                    isLoggingEnabled: true,
                    testDirectory: testDirectory);

                using (var logger = new PluginLogger(environmentVariableReader.Object))
                {
                    Assert.True(logger.IsEnabled);
                }

                environmentVariableReader.VerifyAll();
            }
        }

        [Fact]
        public void Write_WhenLoggingIsNotEnabled_DoesNotCreateLogFile()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var environmentVariableReader = CreateEnvironmentVariableReaderMock(
                    isLoggingEnabled: false,
                    testDirectory: testDirectory);
                var logMessage = new RandomLogMessage();

                using (var logger = new PluginLogger(environmentVariableReader.Object))
                {
                    logger.Write(logMessage);
                }

                var files = Directory.GetFiles(testDirectory.Path, "*", SearchOption.TopDirectoryOnly);

                Assert.Empty(files);

                environmentVariableReader.VerifyAll();
            }
        }

        [Fact]
        public void Write_WhenLoggingIsEnabled_WritesToStringResult()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var environmentVariableReader = CreateEnvironmentVariableReaderMock(
                    isLoggingEnabled: true,
                    testDirectory: testDirectory);
                var logMessage = new RandomLogMessage();

                using (var logger = new PluginLogger(environmentVariableReader.Object))
                {
                    logger.Write(logMessage);
                }

                var logFile = GetLogFile(testDirectory);

                Assert.NotNull(logFile);
                Assert.True(logFile.Exists);

                var actualLines = File.ReadAllLines(logFile.FullName);

                Assert.Collection(actualLines,
                    actualLine =>
                    {
                        var now = DateTimeOffset.UtcNow;

                        var message = VerifyOuterMessageAndReturnInnerMessage(actualLine, now.AddMinutes(-1), now.AddSeconds(1), "stopwatch");

                        Assert.Equal(1, message.Count);
                        Assert.Equal(Stopwatch.Frequency, message["frequency"].Value<long>());
                    },
                    actualLine => Assert.Equal(logMessage.Message, actualLine));

                environmentVariableReader.VerifyAll();
            }
        }

        private static Mock<IEnvironmentVariableReader> CreateEnvironmentVariableReaderMock(
            bool isLoggingEnabled,
            TestDirectory testDirectory = null)
        {
            var environmentVariableReader = new Mock<IEnvironmentVariableReader>(MockBehavior.Strict);

            environmentVariableReader.Setup(x => x.GetEnvironmentVariable(
                    It.Is<string>(value => value == EnvironmentVariableConstants.EnableLog)))
                .Returns(isLoggingEnabled ? bool.TrueString : bool.FalseString);

            if (isLoggingEnabled && testDirectory != null)
            {
                environmentVariableReader.Setup(x => x.GetEnvironmentVariable(
                        It.Is<string>(value => value == EnvironmentVariableConstants.LogDirectoryPath)))
                    .Returns(testDirectory.Path);
            }

            return environmentVariableReader;
        }

        private static FileInfo GetLogFile(TestDirectory directory)
        {
            FileInfo file;

            using (var process = Process.GetCurrentProcess())
            {
                file = new FileInfo(process.MainModule.FileName);
            }

            var partialFileName = $"NuGet_PluginLogFor_{Path.GetFileNameWithoutExtension(file.Name)}_*.log";
            var files = Directory.GetFiles(directory, partialFileName, SearchOption.TopDirectoryOnly);

            Assert.Single(files);

            return new FileInfo(files[0]);
        }

        private sealed class RandomLogMessage : IPluginLogMessage
        {
            internal string Message { get; }

            internal RandomLogMessage()
            {
                Message = Guid.NewGuid().ToString();
            }

            public override string ToString()
            {
                return Message;
            }
        }
    }
}