// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using Moq;
using Xunit;

namespace NuGet.Common.Test
{
    public class CollectorLoggerTests
    {
        [Fact]
        public void CollectorLogger_PassesToInnerLogger()
        {

            // Arrange
            var innerLogger = new Mock<ILogger>();
            var collector = new CollectorLogger(innerLogger.Object, LogLevel.Debug);

            // Act
            collector.LogDebug("Debug");
            collector.LogVerbose("Verbose");
            collector.LogInformation("Information");
            collector.LogWarning("Warning");
            collector.LogError("Error");

            // Assert
            VerifyInnerLoggerCalls(innerLogger, LogLevel.Debug, "Debug", Times.Once());
            VerifyInnerLoggerCalls(innerLogger, LogLevel.Verbose, "Verbose", Times.Once());
            VerifyInnerLoggerCalls(innerLogger, LogLevel.Information, "Information", Times.Once());
            VerifyInnerLoggerCalls(innerLogger, LogLevel.Warning, "Warning", Times.Once());
            VerifyInnerLoggerCalls(innerLogger, LogLevel.Error, "Error", Times.Once());
        }

        [Fact]
        public void CollectorLogger_PassesToInnerLoggerWithLessVerbosity()
        {
            // Arrange
            var innerLogger = new Mock<ILogger>();
            var collector = new CollectorLogger(innerLogger.Object, LogLevel.Verbose);

            // Act
            collector.LogDebug("Debug");
            collector.LogVerbose("Verbose");
            collector.LogInformation("Information");
            collector.LogWarning("Warning");
            collector.LogError("Error");

            // Assert
            VerifyInnerLoggerCalls(innerLogger, LogLevel.Debug, "Debug", Times.Never());
            VerifyInnerLoggerCalls(innerLogger, LogLevel.Verbose, "Verbose", Times.Once());
            VerifyInnerLoggerCalls(innerLogger, LogLevel.Information, "Information", Times.Once());
            VerifyInnerLoggerCalls(innerLogger, LogLevel.Warning, "Warning", Times.Once());
            VerifyInnerLoggerCalls(innerLogger, LogLevel.Error, "Error", Times.Once());
        }

        [Fact]
        public void CollectorLogger_PassesToInnerLoggerWithLeastVerbosity()
        {
            // Arrange
            var innerLogger = new Mock<ILogger>();
            var collector = new CollectorLogger(innerLogger.Object, LogLevel.Error);

            // Act
            collector.LogDebug("Debug");
            collector.LogVerbose("Verbose");
            collector.LogInformation("Information");
            collector.LogWarning("Warning");
            collector.LogError("Error");

            // Assert
            VerifyInnerLoggerCalls(innerLogger, LogLevel.Debug, "Debug", Times.Never());
            VerifyInnerLoggerCalls(innerLogger, LogLevel.Verbose, "Verbose", Times.Never());
            VerifyInnerLoggerCalls(innerLogger, LogLevel.Information, "Information", Times.Never());
            VerifyInnerLoggerCalls(innerLogger, LogLevel.Warning, "Warning", Times.Never());
            VerifyInnerLoggerCalls(innerLogger, LogLevel.Error, "Error", Times.Once());
        }

        [Fact]
        public void CollectorLogger_CollectsErrors()
        {
            // Arrange
            var innerLogger = new Mock<ILogger>();
            var collector = new CollectorLogger(innerLogger.Object);

            // Act
            var errorsStart = collector.Errors.ToArray();
            collector.LogError("ErrorA");
            var errorsA = collector.Errors.ToArray();
            collector.LogError("ErrorB");
            collector.LogError("ErrorC");
            var errorsAbc = collector.Errors.ToArray();
            var errordEnd = collector.Errors.ToArray();

            // Assert
            Assert.Empty(errorsStart);
            Assert.Equal(new[] { "NU1000: ErrorA" }, errorsA.Select(e => e.FormatMessage()));
            Assert.Equal(new[] { "NU1000: ErrorA", "NU1000: ErrorB", "NU1000: ErrorC" }, errorsAbc.Select(e => e.FormatMessage()));
            Assert.Equal(new[] { "NU1000: ErrorA", "NU1000: ErrorB", "NU1000: ErrorC" }, errordEnd.Select(e => e.FormatMessage()));
        }

        [Fact]
        public void CollectorLogger_DoesNotCollectNonErrorAndNonWarnings()
        {
            // Arrange
            var innerLogger = new Mock<ILogger>();
            var collector = new CollectorLogger(innerLogger.Object);

            // Act
            collector.LogDebug("Debug");
            collector.LogVerbose("Verbose");
            collector.LogInformation("Information");
            var errors = collector.Errors.ToArray();

            // Assert
            Assert.Empty(errors);
        }

        private void VerifyInnerLoggerCalls(Mock<ILogger> innerLogger, LogLevel messageLevel, string message, Times times)
        {
            innerLogger.Verify(x => x.Log(It.Is<RestoreLogMessage>(l => l.Code == NuGetLogCode.NU1000 && 
            l.Level == messageLevel && 
            l.Message == message)), 
            times);
        }
    }
}
