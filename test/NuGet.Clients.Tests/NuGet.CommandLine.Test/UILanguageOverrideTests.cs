// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class UILanguageOverrideTests
    {
        [Theory]
        [InlineData("en-US", "en-US")]
        [InlineData("fr-FR", "fr-FR")]
        [InlineData("pt-BR", "pt-BR")]
        public void GetOverriddenUILanguage_WithEnvironmentVariableSet_ReturnsCultureInfo(string envvarValue, string cultureName)
        {
            // Arrange
            Environment.SetEnvironmentVariable("NUGET_CLI_LANGUAGE", envvarValue, EnvironmentVariableTarget.Process);

            // Act
            CultureInfo cultureInfo = UILanguageOverride.GetOverriddenUILanguage();

            // Assert
            Assert.Equal(cultureName, cultureInfo.Name);
        }

        [Fact]
        public void Setup_NullArgument_Throws()
        {
            // Arrange, Act and Assert
            Assert.Throws<ArgumentNullException>(() => UILanguageOverride.Setup(logger: null));
        }

        [Fact]
        public void GetOverriddenUILanguage_WithInvalidValue_LogsErrorMessage()
        {
            // Arrange
            var logger = new TestLogger();
            UILanguageOverride.Setup(logger, (cultureInfo) => { });
            var invalidValue = "invalid-value";
            Environment.SetEnvironmentVariable("NUGET_CLI_LANGUAGE", invalidValue, EnvironmentVariableTarget.Process);

            // Act
            CultureInfo cultureInfo = UILanguageOverride.GetOverriddenUILanguage();

            // Assert
            Assert.Null(cultureInfo);
            string errorMessage = string.Format(CultureInfo.InvariantCulture, NuGet.CommandLine.NuGetResources.Error_InvalidCultureInfo, "NUGET_CLI_LANGUAGE", invalidValue);
            Assert.Collection(logger.Messages, msg => Assert.Equal(errorMessage, msg));
        }
    }
}
