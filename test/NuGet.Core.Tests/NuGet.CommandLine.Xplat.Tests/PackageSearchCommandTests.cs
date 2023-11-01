// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;
using Moq;
using NuGet.CommandLine.XPlat;
using Xunit;
using static NuGet.CommandLine.XPlat.PackageSearchCommand;

namespace NuGet.CommandLine.Xplat.Tests
{
    public class PackageSearchCommandTests
    {
        private CommandLineApplication _app;
        private Func<ILoggerWithColor> _getLogger;
        private PackageSearchArgs _capturedArgs;
        private SetupSettingsAndRunSearchAsyncDelegate _setupSettingsAndRunSearchAsyncDelegate;
        private string _storedErrorMessage;

        public void SetUp()
        {
            _storedErrorMessage = string.Empty;
            _app = new CommandLineApplication();
            var loggerWithColorMock = new Mock<ILoggerWithColor>();
            loggerWithColorMock
            .Setup(x => x.LogError(It.IsAny<string>()))
            .Callback<string>((message) => {
                _storedErrorMessage += message;
            });

            _getLogger = () => loggerWithColorMock.Object;
            _capturedArgs = null;
            async Task<int> SetupSettingsAndRunSearchAsync(PackageSearchArgs args)
            {
                _capturedArgs = args;
                await Task.CompletedTask;
                return 0;
            }

            _setupSettingsAndRunSearchAsyncDelegate = SetupSettingsAndRunSearchAsync;
        }

        [Fact]
        public void Register_withSearchTermOnly_SetsSearchTerm()
        {
            // Arrange
            SetUp();
            Register(_app, _getLogger, _setupSettingsAndRunSearchAsyncDelegate);
            string searchTerm = "nuget";

            // Act
            _app.Execute(new[] { "search", searchTerm });

            //Assert
            Assert.Equal(searchTerm, _capturedArgs.SearchTerm);
        }

        [Fact]
        public void Register_withSingleSourceOption_SetsSources()
        {
            // Arrange
            SetUp();
            Register(_app, _getLogger, _setupSettingsAndRunSearchAsyncDelegate);
            string searchTerm = "nuget";
            string source = "testSource";

            // Act
            _app.Execute(new[] { "search", searchTerm, "--source", source });

            //Assert
            Assert.Contains(source, _capturedArgs.Sources);
        }

        [Fact]
        public void Register_withMultipleSourceOptions_SetsSources()
        {
            // Arrange
            SetUp();
            Register(_app, _getLogger, _setupSettingsAndRunSearchAsyncDelegate);
            string searchTerm = "nuget";
            string source1 = "testSource1";
            string source2 = "testSource2";

            // Act
            _app.Execute(new[] { "search", searchTerm, "--source", source1, "--source", source2 });

            //Assert
            Assert.Contains(source1, _capturedArgs.Sources);
            Assert.Contains(source2, _capturedArgs.Sources);
        }

        [Fact]
        public void Register_withExactMatchOption_SetsExactMatch()
        {
            // Arrange
            SetUp();
            Register(_app, _getLogger, _setupSettingsAndRunSearchAsyncDelegate);
            string searchTerm = "nuget";

            // Act
            _app.Execute(new[] { "search", searchTerm, "--exact-match" });

            //Assert
            Assert.True(_capturedArgs.ExactMatch);
        }

        [Fact]
        public void Register_withPrereleaseOption_SetsPrerelease()
        {
            // Arrange
            SetUp();
            Register(_app, _getLogger, _setupSettingsAndRunSearchAsyncDelegate);
            string searchTerm = "nuget";

            // Act
            _app.Execute(new[] { "search", searchTerm, "--prerelease" });

            //Assert
            Assert.True(_capturedArgs.Prerelease);
        }

        [Fact]
        public void Register_withInteractiveOption_SetsInteractive()
        {
            // Arrange
            SetUp();
            Register(_app, _getLogger, _setupSettingsAndRunSearchAsyncDelegate);
            string searchTerm = "nuget";

            // Act
            _app.Execute(new[] { "search", searchTerm, "--interactive" });

            //Assert
            Assert.True(_capturedArgs.Interactive);
        }

        [Fact]
        public void Register_withTakeOption_SetsTake()
        {
            // Arrange
            SetUp();
            Register(_app, _getLogger, _setupSettingsAndRunSearchAsyncDelegate);
            string searchTerm = "nuget";
            string take = "5";

            // Act
            _app.Execute(new[] { "search", searchTerm, "--take", take });

            //Assert
            Assert.Equal(int.Parse(take), _capturedArgs.Take);
        }

        [Fact]
        public void Register_withSkipOption_SetsSkip()
        {
            // Arrange
            SetUp();
            Register(_app, _getLogger, _setupSettingsAndRunSearchAsyncDelegate);
            string searchTerm = "nuget";
            string skip = "3";

            // Act
            _app.Execute(new[] { "search", searchTerm, "--skip", skip });

            //Assert
            Assert.Equal(int.Parse(skip), _capturedArgs.Skip);
        }

        [Fact]
        public void Register_withInvalidTakeOption_ShowsErrorMessage()
        {
            // Arrange
            SetUp();
            Register(_app, _getLogger, _setupSettingsAndRunSearchAsyncDelegate);
            string searchTerm = "nuget";
            string take = "invalid";
            string expectedError = string.Format(CultureInfo.CurrentCulture, Strings.Error_invalid_number, take);

            // Act
            var exitCode = _app.Execute(new[] { "search", searchTerm, "--take", take });

            // Assert
            Assert.Equal(1, exitCode);
            Assert.Contains(expectedError, _storedErrorMessage);
        }

        [Fact]
        public void Register_withInvalidSkipOption_ShowsErrorMessage()
        {
            // Arrange
            SetUp();
            Register(_app, _getLogger, _setupSettingsAndRunSearchAsyncDelegate);
            string searchTerm = "nuget";
            string skip = "invalid";
            string expectedError = string.Format(CultureInfo.CurrentCulture, Strings.Error_invalid_number, skip);

            // Act
            var exitCode = _app.Execute(new[] { "search", searchTerm, "--skip", skip });

            // Assert
            Assert.Equal(1, exitCode);
            Assert.Contains(expectedError, _storedErrorMessage);
        }

        [Theory]
        [InlineData(new string[] { "search", "nuget", "--exact-match" }, true, false, false)]
        [InlineData(new string[] { "search", "nuget", "--prerelease" }, false, true, false)]
        [InlineData(new string[] { "search", "nuget", "--interactive" }, false, false, true)]
        [InlineData(new string[] { "search", "nuget", "--take", "5" }, false, false, false, 5, 0)]
        [InlineData(new string[] { "search", "nuget", "--skip", "3" }, false, false, false, 20, 3)]
        public void Register_WithOptions_SetsExpectedValues(string[] args, bool expectedExactMatch, bool expectedPrerelease, bool expectedInteractive, int expectedTake = 20, int expectedSkip = 0)
        {
            // Arrange
            SetUp();
            Register(_app, _getLogger, _setupSettingsAndRunSearchAsyncDelegate);

            // Act
            _app.Execute(args);

            // Assert
            Assert.Equal(expectedExactMatch, _capturedArgs.ExactMatch);
            Assert.Equal(expectedPrerelease, _capturedArgs.Prerelease);
            Assert.Equal(expectedInteractive, _capturedArgs.Interactive);
            Assert.Equal(expectedTake, _capturedArgs.Take);
            Assert.Equal(expectedSkip, _capturedArgs.Skip);
        }
    }
}
