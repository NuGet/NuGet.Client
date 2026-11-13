// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Globalization;
using FluentAssertions;
using Microsoft.VisualStudio.Shell.TableManager;
using NuGet.Common;
using Xunit;

namespace NuGet.VisualStudio.Common.Test
{
    public class ErrorListTableEntryTests
    {
        private const string _testMessage = "test log message";
        private const NuGetLogCode _testCode = NuGetLogCode.NU1000;
        private const string _testCodeString = "NU1000";
        private const string _testHelpLink = "https://docs.microsoft.com/nuget/reference/errors-and-warnings/NU1000";
        private const string _testProjectPath = @"unit\test\project.csproj";
        private const string _testProjectName = "project";
        private const int _testLineNumber = 100;
        private const int _testColumnNumber = 50;

        public ErrorListTableEntryTests()
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-US");
        }

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
        [InlineData(StandardTableKeyNames.Text)]
        [InlineData(StandardTableKeyNames.ErrorSeverity)]
        [InlineData(StandardTableKeyNames.DocumentName)]
        [InlineData(StandardTableKeyNames.ErrorCode)]
        [InlineData(StandardTableKeyNames.ProjectName)]
        [InlineData(StandardTableKeyNames.Line)]
        [InlineData(StandardTableKeyNames.Column)]
        [InlineData(StandardTableKeyNames.Priority)]
        [InlineData(StandardTableKeyNames.ErrorSource)]
        [InlineData(StandardTableKeyNames.ErrorCodeToolTip)]
        [InlineData(StandardTableKeyNames.HelpKeyword)]
        [InlineData(StandardTableKeyNames.HelpLink)]
        public void CanSetValue_AlwaysReturnsFalse(string key)
        {
            // Arrange & Act
            var entry = new ErrorListTableEntry(_testMessage, LogLevel.Debug);

            // Assert
            entry.Should().NotBeNull();
            entry.CanSetValue(key).Should().BeFalse();
        }

        [Theory]
        [InlineData(StandardTableKeyNames.Text, _testMessage)]
        [InlineData(StandardTableKeyNames.DocumentName, _testProjectPath)]
        [InlineData(StandardTableKeyNames.ProjectName, _testProjectName)]
        [InlineData(StandardTableKeyNames.Priority, "high")]
        [InlineData(StandardTableKeyNames.ErrorSource, "NuGet")]
        [InlineData(StandardTableKeyNames.ErrorCodeToolTip, _testHelpLink)]
        [InlineData(StandardTableKeyNames.HelpLink, _testHelpLink)]
        [InlineData(StandardTableKeyNames.ErrorCode, _testCodeString)]
        [InlineData(StandardTableKeyNames.HelpKeyword, _testCodeString)]
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
        [InlineData(StandardTableKeyNames.DocumentName)]
        [InlineData(StandardTableKeyNames.ProjectName)]
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
        [InlineData(StandardTableKeyNames.DocumentName, _testProjectPath)]
        [InlineData(StandardTableKeyNames.ProjectName, _testProjectName)]
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
        [InlineData(StandardTableKeyNames.Line, _testLineNumber - 1)]
        [InlineData(StandardTableKeyNames.Column, _testColumnNumber - 1)]
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
        [InlineData(StandardTableKeyNames.Line)]
        [InlineData(StandardTableKeyNames.Column)]
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

        [Theory]
        [InlineData(1, 1, 0, 0)]
        [InlineData(12, 5, 11, 4)]
        [InlineData(0, 0, 0, 0)]
        public void TryGetValue_SourceDocument_PreservesProjectIdentity(int line, int column, int expectedLine, int expectedColumn)
        {
            var entry = new ErrorListTableEntry(new RestoreLogMessage(LogLevel.Warning, NuGetLogCode.NU1603, _testMessage)
            {
                ProjectPath = _testProjectPath,
                FilePath = @"unit\test\references%20.props",
                StartLineNumber = line,
                StartColumnNumber = column
            });

            entry.TryGetValue(StandardTableKeyNames.DocumentName, out var document).Should().BeTrue();
            document.Should().Be(@"unit\test\references%20.props");
            entry.TryGetValue(StandardTableKeyNames.ProjectName, out var project).Should().BeTrue();
            project.Should().Be(_testProjectName);
            entry.TryGetValue(StandardTableKeyNames.Line, out var actualLine).Should().BeTrue();
            actualLine.Should().Be(expectedLine);
            entry.TryGetValue(StandardTableKeyNames.Column, out var actualColumn).Should().BeTrue();
            actualColumn.Should().Be(expectedColumn);
        }
    }
}
