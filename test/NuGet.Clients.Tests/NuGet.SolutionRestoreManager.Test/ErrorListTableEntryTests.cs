// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using FluentAssertions;
using Microsoft.VisualStudio.Shell.TableControl;
using NuGet.Common;
using Xunit;

namespace NuGet.SolutionRestoreManager.Test
{
    public class ErrorListTableEntryTests
    {
        private const string _testMessage = "test log message";
        private const NuGetLogCode _testCode = NuGetLogCode.NU1000;
        private const string _testProjectPath = @"unit\test\project.csproj";
        private const string _testProjectName = "project";
        private const int _testLineNumber = 100;
        private const int _testColumnNumber = 50;

        [Fact]
        public void LogMessageConstructor_KeepsLogMessage()
        {
            // Arrange
            var logMessage = new LogMessage(LogLevel.Debug, _testMessage, _testCode);

            // Act
            var entry = new ErrorListTableEntry(logMessage);

            // Assert
            entry.Should().NotBeNull();
            entry.Message.Should().Be(logMessage);
        }

        [Fact]
        public void StringConstructor_CreatesLogMessage()
        {
            // Arrange & Act
            var entry = new ErrorListTableEntry(_testMessage, LogLevel.Debug);

            // Assert
            entry.Should().NotBeNull();
            entry.Message.Should().NotBeNull();
            entry.Message.Message.Should().Be(_testMessage);
            entry.Message.Code.Should().Be(NuGetLogCode.Undefined);
            entry.Message.Level.Should().Be(LogLevel.Debug);
        }

        [Theory]
        [InlineData("")]
        [InlineData(StandardTableColumnDefinitions.Text)]
        [InlineData(StandardTableColumnDefinitions.ErrorSeverity)]
        [InlineData(StandardTableColumnDefinitions.DocumentName)]
        [InlineData(StandardTableColumnDefinitions.ErrorCode)]
        [InlineData(StandardTableColumnDefinitions.ProjectName)]
        [InlineData(StandardTableColumnDefinitions.Line)]
        [InlineData(StandardTableColumnDefinitions.Column)]
        [InlineData(StandardTableColumnDefinitions.Priority)]
        [InlineData(StandardTableColumnDefinitions.ErrorSource)]
        public void CanSetValue_AlwaysReturnsFalse(string key)
        {
            // Arrange & Act
            var entry = new ErrorListTableEntry(_testMessage, LogLevel.Debug);

            // Assert
            entry.Should().NotBeNull();
            entry.CanSetValue(key).Should().BeFalse();
        }

        [Theory]
        [InlineData(StandardTableColumnDefinitions.Text, _testMessage)]
        [InlineData(StandardTableColumnDefinitions.DocumentName, _testProjectPath)]
        [InlineData(StandardTableColumnDefinitions.ProjectName, _testProjectName)]
        [InlineData(StandardTableColumnDefinitions.Priority, "high")]
        [InlineData(StandardTableColumnDefinitions.ErrorSource, "NuGet")]
        public void TryGetValue_StringContent_ReturnsValues(string key, string content)
        {
            // Arrange
            var logMessage = new RestoreLogMessage(LogLevel.Error, _testCode, _testMessage)
            {
                ProjectPath = _testProjectPath
            };

            // Act
            var entry = new ErrorListTableEntry(logMessage);

            // Assert
            entry.Should().NotBeNull();
            entry.TryGetValue(key, out var result).Should().BeTrue();
            (result is string).Should().BeTrue();
            (result as string).Should().Be(content);
        }

        [Theory]
        [InlineData(StandardTableColumnDefinitions.DocumentName)]
        [InlineData(StandardTableColumnDefinitions.ProjectName)]
        public void TryGetValue_StringContentNotAvailable_ReturnsNull(string key)
        {
            // Arrange
            var logMessage = new RestoreLogMessage(LogLevel.Error, _testCode, _testMessage);

            // Act
            var entry = new ErrorListTableEntry(logMessage);

            // Assert
            entry.Should().NotBeNull();
            entry.TryGetValue(key, out var result).Should().BeFalse();
            result.Should().BeNull();
        }

        [Theory]
        [InlineData(StandardTableColumnDefinitions.DocumentName, _testProjectPath)]
        [InlineData(StandardTableColumnDefinitions.ProjectName, _testProjectName)]
        public void TryGetValue_ProjectPathNull_ReturnsFilePath(string key, string content)
        {
            // Arrange
            var logMessage = new RestoreLogMessage(LogLevel.Error, _testCode, _testMessage)
            {
                FilePath = _testProjectPath
            };

            // Act
            var entry = new ErrorListTableEntry(logMessage);

            // Assert
            entry.Should().NotBeNull();
            entry.TryGetValue(key, out var result).Should().BeTrue();
            (result is string).Should().BeTrue();
            (result as string).Should().Be(content);
        }

        [Theory]
        [InlineData(StandardTableColumnDefinitions.Line, _testLineNumber)]
        [InlineData(StandardTableColumnDefinitions.Column, _testColumnNumber)]
        public void TryGetValue_IntContent_ReturnsValues(string key, int content)
        {
            // Arrange
            var logMessage = new RestoreLogMessage(LogLevel.Error, _testCode, _testMessage)
            {
                StartLineNumber = _testLineNumber,
                StartColumnNumber = _testColumnNumber
            };

            // Act
            var entry = new ErrorListTableEntry(logMessage);

            // Assert
            entry.Should().NotBeNull();
            entry.TryGetValue(key, out var result).Should().BeTrue();
            (result is int).Should().BeTrue();
            ((int)result).Should().Be(content);
        }

        [Theory]
        [InlineData(StandardTableColumnDefinitions.Line)]
        [InlineData(StandardTableColumnDefinitions.Column)]
        public void TryGetValue_IntContentNotAvailable_ReturnsZero(string key)
        {
            // Arrange
            var logMessage = new LogMessage(LogLevel.Error, _testMessage, _testCode);

            // Act
            var entry = new ErrorListTableEntry(logMessage);

            // Assert
            entry.Should().NotBeNull();
            entry.TryGetValue(key, out var result).Should().BeFalse();
            result.Should().BeNull();
        }
    }
}
