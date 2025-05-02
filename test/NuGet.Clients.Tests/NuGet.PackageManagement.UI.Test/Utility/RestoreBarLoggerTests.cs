// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NuGet.Common;
using NuGet.PackageManagement.UI.Utility;
using NuGet.ProjectManagement;
using Xunit;

namespace NuGet.PackageManagement.UI.Test.Utility
{
    public class RestoreBarLoggerTests
    {
        [Fact]
        public void Constructor_NullParameterNuGetProjectContext_Throws()
        {
            // Arrange
            INuGetProjectContext? nuGetProjectContext = null;

            // Act
            Action act = () => new RestoreBarLogger(nuGetProjectContext!);

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("nuGetProjectContext");
        }

        [Fact]
        public async Task LogAsync_ShouldThrowNotImplementedException_ThrowsAsync()
        {
            // Arrange
            var nuGetProjectContext = Mock.Of<INuGetProjectContext>();
            var message = Mock.Of<ILogMessage>();
            var restoreBarLogger = new RestoreBarLogger(nuGetProjectContext);

            // Act
            var act = async () => await restoreBarLogger.LogAsync(message);

            // Assert
            await act.Should().ThrowAsync<NotImplementedException>();
        }

        [Fact]
        public void Log_InvokesNuGetProjectContextLog_Always()
        {
            // Arrange
            var mockNuGetProjectContext = new Mock<INuGetProjectContext>();
            var message = Mock.Of<ILogMessage>();

            // Act
            var restoreBarLogger = new RestoreBarLogger(mockNuGetProjectContext.Object);
            restoreBarLogger.Log(message);

            // Assert
            mockNuGetProjectContext.Verify(nuGetProjectContext => nuGetProjectContext.Log(It.IsAny<ILogMessage>()), Times.Once());
        }

        [Fact]
        public void LogError_InvokesNuGetProjectContextLog_Always()
        {
            // Arrange
            var mockNuGetProjectContext = new Mock<INuGetProjectContext>();
            var message = Mock.Of<ILogMessage>();

            // Act
            var restoreBarLogger = new RestoreBarLogger(mockNuGetProjectContext.Object);
            restoreBarLogger.LogError(data: "Message should be logged");

            // Assert
            mockNuGetProjectContext.Verify(nuGetProjectContext => nuGetProjectContext.Log(It.IsAny<ILogMessage>()), Times.Once());
        }

        [Fact]
        public void LogWarning_InvokesNuGetProjectContextLog_Always()
        {
            // Arrange
            var mockNuGetProjectContext = new Mock<INuGetProjectContext>();
            var message = Mock.Of<ILogMessage>();

            // Act
            var restoreBarLogger = new RestoreBarLogger(mockNuGetProjectContext.Object);
            restoreBarLogger.LogWarning(data: "Message should be logged");

            // Assert
            mockNuGetProjectContext.Verify(nuGetProjectContext => nuGetProjectContext.Log(It.IsAny<ILogMessage>()), Times.Once());
        }

        [Fact]
        public void LogInformation_InvokesNuGetProjectContextLog_Never()
        {
            // Arrange
            var mockNuGetProjectContext = new Mock<INuGetProjectContext>();

            // Act
            var restoreBarLogger = new RestoreBarLogger(mockNuGetProjectContext.Object);
            restoreBarLogger.LogInformation(data: "Message which should be ignored");

            // Assert
            mockNuGetProjectContext.Verify(nuGetProjectContext => nuGetProjectContext.Log(It.IsAny<ILogMessage>()), Times.Never());
        }

        [Fact]
        public void LogInformationSummary_InvokesNuGetProjectContextLog_Never()
        {
            // Arrange
            var mockNuGetProjectContext = new Mock<INuGetProjectContext>();

            // Act
            var restoreBarLogger = new RestoreBarLogger(mockNuGetProjectContext.Object);
            restoreBarLogger.LogInformationSummary(data: "Message which should be ignored");

            // Assert
            mockNuGetProjectContext.Verify(nuGetProjectContext => nuGetProjectContext.Log(It.IsAny<ILogMessage>()), Times.Never());
        }

        [Fact]
        public void LogDebug_InvokesNuGetProjectContextLog_Never()
        {
            // Arrange
            var mockNuGetProjectContext = new Mock<INuGetProjectContext>();

            // Act
            var restoreBarLogger = new RestoreBarLogger(mockNuGetProjectContext.Object);
            restoreBarLogger.LogDebug(data: "Message which should be ignored");

            // Assert
            mockNuGetProjectContext.Verify(nuGetProjectContext => nuGetProjectContext.Log(It.IsAny<ILogMessage>()), Times.Never());
        }


        [Fact]
        public void LogMinimal_InvokesNuGetProjectContextLog_Never()
        {
            // Arrange
            var mockNuGetProjectContext = new Mock<INuGetProjectContext>();

            // Act
            var restoreBarLogger = new RestoreBarLogger(mockNuGetProjectContext.Object);
            restoreBarLogger.LogMinimal(data: "Message which should be ignored");

            // Assert
            mockNuGetProjectContext.Verify(nuGetProjectContext => nuGetProjectContext.Log(It.IsAny<ILogMessage>()), Times.Never());
        }

        [Fact]
        public void LogVerbose_InvokesNuGetProjectContextLog_Never()
        {
            // Arrange
            var mockNuGetProjectContext = new Mock<INuGetProjectContext>();

            // Act
            var restoreBarLogger = new RestoreBarLogger(mockNuGetProjectContext.Object);
            restoreBarLogger.LogVerbose(data: "Message which should be ignored");

            // Assert
            mockNuGetProjectContext.Verify(nuGetProjectContext => nuGetProjectContext.Log(It.IsAny<ILogMessage>()), Times.Never());
        }
    }
}
