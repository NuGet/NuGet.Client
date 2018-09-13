// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
            var collector = new CollectorLogger(innerLogger.Object);

            // Act
            collector.LogDebug("Debug");
            collector.LogVerbose("Verbose");
            collector.LogInformation("Information");
            collector.LogWarning("Warning");
            collector.LogError("Error");

            // Assert
            innerLogger.Verify(x => x.LogDebug("Debug"), Times.Once);
            innerLogger.Verify(x => x.LogVerbose("Verbose"), Times.Once);
            innerLogger.Verify(x => x.LogInformation("Information"), Times.Once);
            innerLogger.Verify(x => x.LogWarning("Warning"), Times.Once);
            innerLogger.Verify(x => x.LogError("Error"), Times.Once);
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
            Assert.Equal(new[] { "ErrorA" }, errorsA);
            Assert.Equal(new[] { "ErrorA", "ErrorB", "ErrorC" }, errorsAbc);
            Assert.Equal(new[] { "ErrorA", "ErrorB", "ErrorC" }, errordEnd);
        }

        [Fact]
        public void CollectorLogger_DoesNotCollectNonError()
        {
            // Arrange
            var innerLogger = new Mock<ILogger>();
            var collector = new CollectorLogger(innerLogger.Object);

            // Act
            collector.LogDebug("Debug");
            collector.LogVerbose("Verbose");
            collector.LogInformation("Information");
            collector.LogWarning("Warning");
            var errors = collector.Errors.ToArray();

            // Assert
            Assert.Empty(errors);
        }
    }
}
