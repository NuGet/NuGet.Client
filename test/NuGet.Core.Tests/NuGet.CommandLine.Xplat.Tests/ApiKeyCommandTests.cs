// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using NuGet.CommandLine.XPlat;
using NuGet.Configuration;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Xplat.Tests
{
    public class ApiKeyCommandTests
    {
        private const string SourceName = "contoso";
        private const string SourceUrl = "https://nuget.contoso.org/v3/index.json";

        [PlatformFact(Platform.Windows)]
        public void SetApiKey_WithDefaultSource_SavesApiKey()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                string configFile = CreateEmptyConfig(testDirectory);
                string apiKey = Guid.NewGuid().ToString();
                var logger = new TestLogger();
                var args = new ApiKeySetArgs
                {
                    ApiKey = apiKey,
                    ConfigFile = configFile
                };

                int exitCode = ApiKeySetRunner.Run(args, () => logger);

                Assert.Equal(ExitCodes.Success, exitCode);
                Assert.Contains(logger.MinimalMessages, message => message.Contains(NuGetConstants.DefaultGalleryServerUrl));
                ISettings settings = LoadSettings(configFile);
                string? actualApiKey = SettingsUtility.GetDecryptedValueForAddItem(
                    settings,
                    ConfigurationConstants.ApiKeys,
                    NuGetConstants.DefaultGalleryServerUrl);
                Assert.Equal(apiKey, actualApiKey);
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData(SourceName)]
        [InlineData(SourceUrl)]
        public void SetApiKey_WithSpecifiedSource_SavesApiKeyForResolvedSource(string source)
        {
            using (var testDirectory = TestDirectory.Create())
            {
                string configFile = CreateConfigWithSource(testDirectory);
                string apiKey = Guid.NewGuid().ToString();
                var logger = new TestLogger();
                var args = new ApiKeySetArgs
                {
                    ApiKey = apiKey,
                    Source = source,
                    ConfigFile = configFile
                };

                int exitCode = ApiKeySetRunner.Run(args, () => logger);

                Assert.Equal(ExitCodes.Success, exitCode);
                Assert.Contains(logger.MinimalMessages, message => message.Contains(SourceUrl));
                ISettings settings = LoadSettings(configFile);
                string? actualApiKey = SettingsUtility.GetDecryptedValueForAddItem(
                    settings,
                    ConfigurationConstants.ApiKeys,
                    SourceUrl);
                Assert.Equal(apiKey, actualApiKey);
            }
        }

        [PlatformFact(Platform.Windows)]
        public void UnsetApiKey_WithSpecifiedSource_RemovesApiKey()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                string configFile = CreateConfigWithSource(testDirectory);
                string apiKey = Guid.NewGuid().ToString();
                var logger = new TestLogger();
                var setArgs = new ApiKeySetArgs
                {
                    ApiKey = apiKey,
                    Source = SourceName,
                    ConfigFile = configFile
                };
                var unsetArgs = new ApiKeyUnsetArgs
                {
                    Source = SourceName,
                    ConfigFile = configFile
                };

                ApiKeySetRunner.Run(setArgs, () => logger);
                int exitCode = ApiKeyUnsetRunner.Run(unsetArgs, () => logger);

                Assert.Equal(ExitCodes.Success, exitCode);
                Assert.Contains(logger.MinimalMessages, message => message.Contains(SourceUrl));
                ISettings settings = LoadSettings(configFile);
                string? actualApiKey = SettingsUtility.GetDecryptedValueForAddItem(
                    settings,
                    ConfigurationConstants.ApiKeys,
                    SourceUrl);
                Assert.Null(actualApiKey);
            }
        }

        [Fact]
        public void UnsetApiKey_WhenApiKeyDoesNotExist_ReportsMissingApiKey()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                string configFile = CreateConfigWithSource(testDirectory);
                var logger = new TestLogger();
                var args = new ApiKeyUnsetArgs
                {
                    Source = SourceName,
                    ConfigFile = configFile
                };

                int exitCode = ApiKeyUnsetRunner.Run(args, () => logger);

                Assert.Equal(ExitCodes.Success, exitCode);
                Assert.Contains(logger.MinimalMessages, message => message.Contains("No API key was found"));
                Assert.Contains(logger.MinimalMessages, message => message.Contains(SourceUrl));
            }
        }

        [Fact]
        public void SetApiKey_WithMissingApiKey_ReturnsInvalidArguments()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                string configFile = CreateEmptyConfig(testDirectory);
                var logger = new TestLogger();
                var args = new ApiKeySetArgs
                {
                    ConfigFile = configFile
                };

                int exitCode = ApiKeySetRunner.Run(args, () => logger);

                Assert.Equal(ExitCodes.InvalidArguments, exitCode);
                Assert.Contains(logger.ErrorMessages, message => message.Contains("Please provide an API key"));
            }
        }

        private static string CreateEmptyConfig(TestDirectory testDirectory)
        {
            string configFile = Path.Combine(testDirectory, "nuget.config");
            File.WriteAllText(configFile, "<configuration />");
            return configFile;
        }

        private static string CreateConfigWithSource(TestDirectory testDirectory)
        {
            string configFile = Path.Combine(testDirectory, "nuget.config");
            File.WriteAllText(
                configFile,
                $@"<configuration>
  <packageSources>
    <add key=""{SourceName}"" value=""{SourceUrl}"" />
  </packageSources>
</configuration>");
            return configFile;
        }

        private static ISettings LoadSettings(string configFile)
        {
            return Settings.LoadDefaultSettings(
                Path.GetDirectoryName(configFile),
                Path.GetFileName(configFile),
                machineWideSettings: null);
        }
    }
}
