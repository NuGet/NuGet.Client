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
            Register(RootCommand, GetLogger, SetupSettingsAndRunSearchAsync);
            string searchTerm = "nuget";

            // Act
            RootCommand.Parse(new[] { "search", searchTerm }).Invoke();

            //Assert
            Assert.Equal(searchTerm, CapturedArgs.SearchTerm);
        }

        [Fact]
        public void Register_withSingleSourceOption_SetsSources()
        {
            // Arrange
            Register(RootCommand, GetLogger, SetupSettingsAndRunSearchAsync);
            string searchTerm = "nuget";
            string source = "testSource";

            // Act
            RootCommand.Parse(new[] { "search", searchTerm, "--source", source }).Invoke();

            //Assert
            Assert.Contains(source, CapturedArgs.Sources);
        }

        [Fact]
        public void Register_withMultipleSourceOptions_SetsSources()
        {
            // Arrange
            Register(RootCommand, GetLogger, SetupSettingsAndRunSearchAsync);
            string searchTerm = "nuget";
            string source1 = "testSource1";
            string source2 = "testSource2";

            // Act
            RootCommand.Parse(new[] { "search", searchTerm, "--source", source1, "--source", source2 }).Invoke();

            //Assert
            Assert.Contains(source1, CapturedArgs.Sources);
            Assert.Contains(source2, CapturedArgs.Sources);
        }

        [Fact]
        public void Register_withExactMatchOption_SetsExactMatch()
        {
            // Arrange
            Register(RootCommand, GetLogger, SetupSettingsAndRunSearchAsync);
            string searchTerm = "nuget";

            // Act
            RootCommand.Parse(new[] { "search", searchTerm, "--exact-match" }).Invoke();

            //Assert
            Assert.True(CapturedArgs.ExactMatch);
        }

        [Fact]
        public void Register_withPrereleaseOption_SetsPrerelease()
        {
            // Arrange
            Register(RootCommand, GetLogger, SetupSettingsAndRunSearchAsync);
            string searchTerm = "nuget";

            // Act
            RootCommand.Parse(new[] { "search", searchTerm, "--prerelease" }).Invoke();

            //Assert
            Assert.True(CapturedArgs.Prerelease);
        }

        [Fact]
        public void Register_withInteractiveOption_SetsInteractive()
        {
            // Arrange
            Register(RootCommand, GetLogger, SetupSettingsAndRunSearchAsync);
            string searchTerm = "nuget";

            // Act
            RootCommand.Parse(new[] { "search", searchTerm, "--interactive" }).Invoke();

            //Assert
            Assert.True(CapturedArgs.Interactive);
        }

        [Fact]
        public void Register_withTakeOption_SetsTake()
        {
            // Arrange
            Register(RootCommand, GetLogger, SetupSettingsAndRunSearchAsync);
            string searchTerm = "nuget";
            string take = "5";

            // Act
            RootCommand.Parse(new[] { "search", searchTerm, "--take", take }).Invoke();

            //Assert
            Assert.Equal(int.Parse(take), CapturedArgs.Take);
        }

        [Fact]
        public void Register_withSkipOption_SetsSkip()
        {
            // Arrange
            Register(RootCommand, GetLogger, SetupSettingsAndRunSearchAsync);
            string searchTerm = "nuget";
            string skip = "3";

            // Act
            RootCommand.Parse(new[] { "search", searchTerm, "--skip", skip }).Invoke();

            //Assert
            Assert.Equal(int.Parse(skip), CapturedArgs.Skip);
        }

        [Fact]
        public void Register_withInvalidTakeOption_ShowsErrorMessage()
        {
            // Arrange
            Register(RootCommand, GetLogger, SetupSettingsAndRunSearchAsync);
            string searchTerm = "nuget";
            string take = "invalid";
            string expectedError = string.Format(CultureInfo.CurrentCulture, Strings.Error_invalidOptionValue, take, "--take");

            // Act
            var exitCode = RootCommand.Parse(new[] { "search", searchTerm, "--take", take }).Invoke();

            // Assert
            Assert.Equal(1, exitCode);
            Assert.Contains(expectedError, StoredErrorMessage);
        }

        [Fact]
        public void Register_withInvalidSkipOption_ShowsErrorMessage()
        {
            // Arrange
            Register(RootCommand, GetLogger, SetupSettingsAndRunSearchAsync);
            string searchTerm = "nuget";
            string skip = "invalid";
            string expectedError = string.Format(CultureInfo.CurrentCulture, Strings.Error_invalidOptionValue, skip, "--skip");

            // Act
            var exitCode = RootCommand.Parse(new[] { "search", searchTerm, "--skip", skip }).Invoke();

            // Assert
            Assert.Equal(1, exitCode);
            Assert.Contains(expectedError, StoredErrorMessage);
        }

        [Theory]
        [InlineData("table")]
        [InlineData("json")]
        public void Register_withFormatOption_SetsFormat(string format)
        {
            // Arrange
            Register(RootCommand, GetLogger, SetupSettingsAndRunSearchAsync);

            // Act
            RootCommand.Parse(new[] { "search", "--format", format }).Invoke();

            // Assert
            if (format == "json")
            {
                Assert.True(CapturedArgs.JsonFormat);
            }
            else
            {
                Assert.False(CapturedArgs.JsonFormat);
            }
        }

        [Fact]
        public void Register_withInvalidFormattingOption_DefaultsTable()
        {
            // Arrange
            Register(RootCommand, GetLogger, SetupSettingsAndRunSearchAsync);
            string invalidFormat = "invalid";

            // Act
            var exitCode = RootCommand.Parse(new[] { "search", "--format", invalidFormat }).Invoke();

            // Assert
            Assert.Equal(0, exitCode);
            Assert.False(CapturedArgs.JsonFormat);
        }

        [Theory]
        [InlineData("minimal")]
        [InlineData("normal")]
        [InlineData("detailed")]
        public void Register_withVerbosityOption_SetsFormat(string verbosity)
        {
            // Arrange
            Register(RootCommand, GetLogger, SetupSettingsAndRunSearchAsync);

            // Act
            RootCommand.Parse(new[] { "search", "--verbosity", verbosity }).Invoke();

            // Assert
            if (verbosity == "minimal")
            {
                Assert.Equal(PackageSearchVerbosity.Minimal, CapturedArgs.Verbosity);
            }
            else if (verbosity == "detailed")
            {
                Assert.Equal(PackageSearchVerbosity.Detailed, CapturedArgs.Verbosity);
            }
            else
            {
                Assert.Equal(PackageSearchVerbosity.Normal, CapturedArgs.Verbosity);
            }
        }

        [Fact]
        public void Register_withInvalidVerbosityOption_DefaultsNormal()
        {
            // Arrange
            Register(RootCommand, GetLogger, SetupSettingsAndRunSearchAsync);
            string invalidFormat = "invalid";

            // Act
            var exitCode = RootCommand.Parse(new[] { "search", "--verbosity", invalidFormat }).Invoke();

            // Assert
            Assert.Equal(0, exitCode);
            Assert.Equal(PackageSearchVerbosity.Normal, CapturedArgs.Verbosity);
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
            Register(RootCommand, GetLogger, SetupSettingsAndRunSearchAsync);

            // Act
            RootCommand.Parse(args).Invoke();

            // Assert
            Assert.Equal(expectedExactMatch, CapturedArgs.ExactMatch);
            Assert.Equal(expectedPrerelease, CapturedArgs.Prerelease);
            Assert.Equal(expectedInteractive, CapturedArgs.Interactive);
            Assert.Equal(expectedTake, CapturedArgs.Take);
            Assert.Equal(expectedSkip, CapturedArgs.Skip);
        }
    }
}
