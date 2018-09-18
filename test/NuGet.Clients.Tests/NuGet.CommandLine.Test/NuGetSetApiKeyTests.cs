// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using NuGet.Configuration;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class NuGetSetApiKeyTests
    {
        private static readonly string NuGetExePath = Util.GetNuGetExePath();

        [Fact]
        public void SetApiKey_DefaultSource()
        {
            using (var testFolder = TestDirectory.Create())
            {
                var configFile = Path.Combine(testFolder, "nuget.config");
                Util.CreateFile(configFile, "<configuration/>");

                var testApiKey = Guid.NewGuid().ToString();

                // Act
                var result = CommandRunner.Run(
                    NuGetExePath,
                    testFolder,
                    $"setApiKey {testApiKey} -ConfigFile {configFile}",
                    waitForExit: true);

                // Assert
                Assert.True(0 == result.Item1, $"{result.Item2} {result.Item3}");
                Assert.Contains($"The API Key '{testApiKey}' was saved for the NuGet gallery (https://www.nuget.org) and the symbol server (https://nuget.smbsrc.net/)", result.Item2);

                var settings = Configuration.Settings.LoadDefaultSettings(
                    Path.GetDirectoryName(configFile),
                    Path.GetFileName(configFile),
                    null);

                var actualApiKey = SettingsUtility.GetDecryptedValueForAddItem(settings, ConfigurationConstants.ApiKeys, NuGetConstants.DefaultGalleryServerUrl);
                Assert.NotNull(actualApiKey);
                Assert.Equal(testApiKey, actualApiKey);
            }
        }
    }
}
