// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using NuGet.Configuration;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Xplat.Tests
{
    internal static class CommandTestUtils
    {
        internal static void AssertBothCommandSuccessfulExecution(int statusCurrent, int statusNew, TestLogger loggerCurrent, TestLogger loggerNew)
        {
            Assert.Equal(0, statusCurrent);
            Assert.Equal(0, statusNew);
            Assert.False(loggerCurrent.Messages.IsEmpty);
            Assert.False(loggerNew.Messages.IsEmpty);
            Assert.Equal(loggerCurrent.Messages, loggerNew.Messages);
        }

        internal static SettingSection GetNuGetConfigSection(string configFile, string sectionName)
        {
            string configDirectory = Path.GetDirectoryName(configFile);
            string configFileName = Path.GetFileName(configFile);
            ISettings settings = Settings.LoadSpecificSettings(configDirectory, configFileName);
            SettingSection packageSourcesSection = settings.GetSection(sectionName);

            return packageSourcesSection;
        }

        internal static void AssertDisableSource(string configFile, string sourceName)
        {
            SettingSection disabledSources = CommandTestUtils.GetNuGetConfigSection(configFile, "disabledPackageSources");
            Assert.Collection(disabledSources.Items, elem => Assert.Equal(sourceName, (elem as AddItem).Key));
        }
    }
}
