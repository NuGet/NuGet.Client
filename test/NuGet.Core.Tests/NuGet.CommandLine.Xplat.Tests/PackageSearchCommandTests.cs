// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Globalization;
using NuGet.CommandLine.XPlat;
using Xunit;
using static NuGet.CommandLine.XPlat.PackageSearchCommand;

namespace NuGet.CommandLine.Xplat.Tests
{
    public class PackageSearchCommandTests : PackageSearchTestInitializer
    {
        [Fact]
        public void Register_withSearchTermOnly_SetsSearchTerm()
        {
            // Arrange
            Register(App, GetLogger, SetupSettingsAndRunSearchAsyncDelegate);
            string searchTerm = "nuget";

            // Act
            App.Execute(new[] { "search", searchTerm });

            //Assert
            Assert.Equal(searchTerm, CapturedArgs.SearchTerm);
        }

        [Fact]
        public void Register_withSingleSourceOption_SetsSources()
        {
            // Arrange
            Register(App, GetLogger, SetupSettingsAndRunSearchAsyncDelegate);
            string searchTerm = "nuget";
            string source = "testSource";

            // Act
            App.Execute(new[] { "search", searchTerm, "--source", source });

            //Assert
            Assert.Contains(source, CapturedArgs.Sources);
        }

        [Fact]
        public void Register_withMultipleSourceOptions_SetsSources()
        {
            // Arrange
            Register(App, GetLogger, SetupSettingsAndRunSearchAsyncDelegate);
            string searchTerm = "nuget";
            string source1 = "testSource1";
            string source2 = "testSource2";

            // Act
            App.Execute(new[] { "search", searchTerm, "--source", source1, "--source", source2 });

            //Assert
            Assert.Contains(source1, CapturedArgs.Sources);
            Assert.Contains(source2, CapturedArgs.Sources);
        }

        [Fact]
        public void Register_withExactMatchOption_SetsExactMatch()
        {
            // Arrange
            Register(App, GetLogger, SetupSettingsAndRunSearchAsyncDelegate);
            string searchTerm = "nuget";

            // Act
            App.Execute(new[] { "search", searchTerm, "--exact-match" });

            //Assert
            Assert.True(CapturedArgs.ExactMatch);
        }

        [Fact]
        public void Register_withPrereleaseOption_SetsPrerelease()
        {
            // Arrange
            Register(App, GetLogger, SetupSettingsAndRunSearchAsyncDelegate);
            string searchTerm = "nuget";

            // Act
            App.Execute(new[] { "search", searchTerm, "--prerelease" });

            //Assert
            Assert.True(CapturedArgs.Prerelease);
        }

        [Fact]
        public void Register_withInteractiveOption_SetsInteractive()
        {
            // Arrange
            Register(App, GetLogger, SetupSettingsAndRunSearchAsyncDelegate);
            string searchTerm = "nuget";

            // Act
            App.Execute(new[] { "search", searchTerm, "--interactive" });

            //Assert
            Assert.True(CapturedArgs.Interactive);
        }

        [Fact]
        public void Register_withTakeOption_SetsTake()
        {
            // Arrange
            Register(App, GetLogger, SetupSettingsAndRunSearchAsyncDelegate);
            string searchTerm = "nuget";
            string take = "5";

            // Act
            App.Execute(new[] { "search", searchTerm, "--take", take });

            //Assert
            Assert.Equal(int.Parse(take), CapturedArgs.Take);
        }

        [Fact]
        public void Register_withSkipOption_SetsSkip()
        {
            // Arrange
            Register(App, GetLogger, SetupSettingsAndRunSearchAsyncDelegate);
            string searchTerm = "nuget";
            string skip = "3";

            // Act
            App.Execute(new[] { "search", searchTerm, "--skip", skip });

            //Assert
            Assert.Equal(int.Parse(skip), CapturedArgs.Skip);
        }

        [Fact]
        public void Register_withInvalidTakeOption_ShowsErrorMessage()
        {
            // Arrange
            Register(App, GetLogger, SetupSettingsAndRunSearchAsyncDelegate);
            string searchTerm = "nuget";
            string take = "invalid";
            string expectedError = string.Format(CultureInfo.CurrentCulture, Strings.Error_invalid_number, take);

            // Act
            var exitCode = App.Execute(new[] { "search", searchTerm, "--take", take });

            // Assert
            Assert.Equal(1, exitCode);
            Assert.Contains(expectedError, StoredErrorMessage);
        }

        [Fact]
        public void Register_withInvalidSkipOption_ShowsErrorMessage()
        {
            // Arrange
            Register(App, GetLogger, SetupSettingsAndRunSearchAsyncDelegate);
            string searchTerm = "nuget";
            string skip = "invalid";
            string expectedError = string.Format(CultureInfo.CurrentCulture, Strings.Error_invalid_number, skip);

            // Act
            var exitCode = App.Execute(new[] { "search", searchTerm, "--skip", skip });

            // Assert
            Assert.Equal(1, exitCode);
            Assert.Contains(expectedError, StoredErrorMessage);
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
            Register(App, GetLogger, SetupSettingsAndRunSearchAsyncDelegate);

            // Act
            App.Execute(args);

            // Assert
            Assert.Equal(expectedExactMatch, CapturedArgs.ExactMatch);
            Assert.Equal(expectedPrerelease, CapturedArgs.Prerelease);
            Assert.Equal(expectedInteractive, CapturedArgs.Interactive);
            Assert.Equal(expectedTake, CapturedArgs.Take);
            Assert.Equal(expectedSkip, CapturedArgs.Skip);
        }
    }
}
