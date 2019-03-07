// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using Moq;
using NuGet.Common;
using Xunit;

namespace NuGet.Protocol.Plugins.Tests
{
    public class PluginLoggerTests
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
            var environmentVariableReader = CreateEnvironmentVariableReaderMock(isLoggingEnabled: true);

            using (var logger = new PluginLogger(environmentVariableReader.Object))
            {
                Assert.True(logger.IsEnabled);
            }

            environmentVariableReader.VerifyAll();
        }

        [Fact]
        public void Write_WhenLoggingIsNotEnabled_DoesNotCreateLogFile()
        {
            var environmentVariableReader = CreateEnvironmentVariableReaderMock(isLoggingEnabled: false);
            var logMessage = new RandomLogMessage();
            var logFile = GetLogFile();

            if (logFile.Exists)
            {
                logFile.Delete();
            }

            using (var logger = new PluginLogger(environmentVariableReader.Object))
            {
                logger.Write(logMessage);
            }

            logFile.Refresh();

            Assert.False(logFile.Exists);

            environmentVariableReader.VerifyAll();
        }

        [Fact]
        public void Write_WhenLoggingIsEnabled_WritesToStringResult()
        {
            var environmentVariableReader = CreateEnvironmentVariableReaderMock(isLoggingEnabled: true);
            var logMessage = new RandomLogMessage();
            var logFile = GetLogFile();

            if (logFile.Exists)
            {
                logFile.Delete();
            }

            try
            {
                using (var logger = new PluginLogger(environmentVariableReader.Object))
                {
                    logger.Write(logMessage);
                }

                logFile.Refresh();

                Assert.True(logFile.Exists);

                var actualContent = File.ReadAllText(logFile.FullName);

                Assert.Equal(logMessage.Message + Environment.NewLine, actualContent);
            }
            finally
            {
                if (logFile.Exists)
                {
                    logFile.Delete();
                }
            }


            environmentVariableReader.VerifyAll();
        }

        private static Mock<IEnvironmentVariableReader> CreateEnvironmentVariableReaderMock(bool isLoggingEnabled)
        {
            var environmentVariableReader = new Mock<IEnvironmentVariableReader>(MockBehavior.Strict);

            environmentVariableReader.Setup(x => x.GetEnvironmentVariable(
                    It.Is<string>(value => value == EnvironmentVariableConstants.EnableLog)))
                .Returns(isLoggingEnabled ? bool.TrueString : bool.FalseString);

            return environmentVariableReader;
        }

        private static FileInfo GetLogFile()
        {
            FileInfo file;

            using (var process = Process.GetCurrentProcess())
            {
                file = new FileInfo(process.MainModule.FileName);
            }

            var fileName = $"NuGet_PluginLogFor_{Path.GetFileNameWithoutExtension(file.Name)}.log";

            return new FileInfo(fileName);
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