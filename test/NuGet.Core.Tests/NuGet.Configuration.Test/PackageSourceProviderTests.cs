// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Moq;
using Xunit;

namespace NuGet.Configuration.Test
{
    public class PackageSourceProviderTests
    {
        [Fact]
        public void PrimarySourceIsAddedWhenNotPresent()
        {
            // Act
            NullSettings settings = new NullSettings();
            List<PackageSource> primary = new List<PackageSource>();
            PackageSource item = new PackageSource(NuGetConstants.V3FeedUrl, NuGetConstants.FeedName);
            primary.Add(item);

            List<PackageSource> secondary = new List<PackageSource>();
            PackageSource item2 = new PackageSource(NuGetConstants.V2FeedUrl, NuGetConstants.FeedName, false);
            secondary.Add(item2);

            PackageSourceProvider psp = new PackageSourceProvider(settings, primary, secondary);

            // Act
            var sources = psp.LoadPackageSources();

            // Assert
            //Primary IsEnabled = true, IsOfficial = true (Added for the first time)
            var actual = Assert.Single(sources);
            Assert.Equal(item.Name, actual.Name);
            Assert.Equal(item.Source, actual.Source);
            Assert.True(actual.IsEnabled);
            Assert.True(actual.IsOfficial);
        }

        [Fact]
        public void SecondarySourceIsPreferredOverPrimaryIfItHasAHigherProtocolVersion()
        {
            // Act
            var settings = new NullSettings();
            var primarySource = new PackageSource(NuGetConstants.V3FeedUrl, NuGetConstants.FeedName);

            var secondarySource = new PackageSource(NuGetConstants.V2FeedUrl, NuGetConstants.FeedName);
            secondarySource.ProtocolVersion = 3;

            var packageSourceProvider = new PackageSourceProvider(settings, new[] { primarySource }, new[] { secondarySource });

            // Act
            var sources = packageSourceProvider.LoadPackageSources();

            // Assert
            //Primary IsEnabled = true, IsOfficial = true (Added for the first time)
            var actual = Assert.Single(sources);
            Assert.Equal(secondarySource.Name, actual.Name);
            Assert.Equal(secondarySource.Source, actual.Source);
            Assert.True(actual.IsEnabled);
            Assert.True(actual.IsOfficial);
        }

        [Fact]
        public void WhenPrimarySourcesAreRepeatedWithTheSameProtocolVersion_FIrstSourceIsPicked()
        {
            // Act
            var settings = new NullSettings();
            var primarySource1 = new PackageSource("Source1", "FeedName");
            var primarySource2 = new PackageSource("Source2", "FeedName");
            var primarySource3 = new PackageSource("Source3", "FeedName")
            {
                ProtocolVersion = 3
            };

            var primarySource4 = new PackageSource("Source4", "FeedName")
            {
                ProtocolVersion = 3
            };
            var primarySources = new[]
            {
                primarySource1, primarySource2, primarySource3, primarySource4
            };

            var secondarySource = new PackageSource(NuGetConstants.V2FeedUrl, "FeedName");
            secondarySource.ProtocolVersion = 3;

            var packageSourceProvider = new PackageSourceProvider(settings, primarySources, new[] { secondarySource });

            // Act
            var sources = packageSourceProvider.LoadPackageSources();

            // Assert
            //Primary IsEnabled = true, IsOfficial = true (Added for the first time)
            var actual = Assert.Single(sources);
            Assert.Equal(primarySource3.Name, actual.Name);
            Assert.Equal(primarySource3.Source, actual.Source);
            Assert.True(actual.IsOfficial);
        }

        public void PrimaryURLIsForcedWhenPrimaryNameHasAnotherFeed()
        {
            // Act
            //Create nuget.config that has Primary name used for a different feed
            string nugetConfigFileFolder = TestFilesystemUtility.CreateRandomTestFolder();
            var nugetConfigFilePath = Path.Combine(nugetConfigFileFolder, "nuget.config");
            File.Create(nugetConfigFilePath).Close();

            var randomURL = "https://www.somerandomURL.com/";
            var enabledReplacement = @"<add key='" + NuGetConstants.FeedName + "' value='" + randomURL + "' />";
            var disabledReplacement = string.Empty;
            File.WriteAllText(nugetConfigFilePath, CreateNuGetConfigContent(enabledReplacement, disabledReplacement));

            Settings settings = new Settings(nugetConfigFileFolder, "nuget.config");
            PackageSourceProvider before = new PackageSourceProvider(settings);
            VerifyPackageSource(before, 1, new string[] { NuGetConstants.FeedName },
                new string[] { randomURL },
                new bool[] { true }, new bool[] { false });

            List<PackageSource> primary = new List<PackageSource>();
            PackageSource item = new PackageSource(NuGetConstants.V3FeedUrl, NuGetConstants.FeedName);
            primary.Add(item);

            PackageSourceProvider after = new PackageSourceProvider(settings, primary, null);

            // Assert
            //Primary Name already exists in nuget.config but with different URL
            //It gets overwritten by primary package source (which is enabled above while creating it)
            //IsEnabled = true, IsOfficial = true
            VerifyPackageSource(after, 1, new string[] { NuGetConstants.FeedName },
                new string[] { NuGetConstants.V3FeedUrl },
                new bool[] { false }, new bool[] { true });

            //Clean up
            TestFilesystemUtility.DeleteRandomTestFolders(nugetConfigFileFolder);
        }

        public void SecondaryURLIsForcedWhenSecondaryNameHasAnotherFeed()
        {
            // Act
            //Create nuget.config that has Secondary name used for a different feed
            string nugetConfigFileFolder = TestFilesystemUtility.CreateRandomTestFolder();
            var nugetConfigFilePath = Path.Combine(nugetConfigFileFolder, "nuget.config");
            File.Create(nugetConfigFilePath).Close();

            var randomURL = "https://www.somerandomURL.com/";
            var enabledReplacement = @"<add key='" + NuGetConstants.FeedName + "' value='" + NuGetConstants.V3FeedUrl + "' />";
            enabledReplacement = enabledReplacement + @"<add key='" + NuGetConstants.FeedName + "' value='" + randomURL + "' />";
            var disabledReplacement = string.Empty;
            File.WriteAllText(nugetConfigFilePath, CreateNuGetConfigContent(enabledReplacement, disabledReplacement));

            Settings settings = new Settings(nugetConfigFileFolder, "nuget.config");
            PackageSourceProvider before = new PackageSourceProvider(settings);
            VerifyPackageSource(before, 2, new string[] { NuGetConstants.FeedName, NuGetConstants.FeedName },
                new string[] { NuGetConstants.V3FeedUrl, randomURL },
                new bool[] { true, true }, new bool[] { false, false });

            List<PackageSource> primary = new List<PackageSource>();
            PackageSource item = new PackageSource(NuGetConstants.V3FeedUrl, NuGetConstants.FeedName);
            primary.Add(item);

            List<PackageSource> secondary = new List<PackageSource>();
            PackageSource item2 = new PackageSource(NuGetConstants.V2FeedUrl, NuGetConstants.FeedName, false);
            secondary.Add(item2);

            PackageSourceProvider after = new PackageSourceProvider(settings, primary, secondary);
            // Assert
            //Seconday name already exists in nuget.config but with different URL
            //It gets overwritten by secondary scource which is disabled (while getting created above)
            //IsOfficial is set to true
            VerifyPackageSource(after, 2, new string[] { NuGetConstants.FeedName, NuGetConstants.FeedName },
                new string[] { NuGetConstants.V3FeedUrl, NuGetConstants.V2FeedUrl },
                new bool[] { true, false }, new bool[] { true, true });

            //Clean up
            TestFilesystemUtility.DeleteRandomTestFolders(nugetConfigFileFolder);
        }

        [Fact]
        public void PrimaryNameNotChangedWhenTheFeedHasAnotherName()
        {
            // Act
            //Create nuget.config that has Primary defined (Feed Name is different) and Secondary defined 
            string nugetConfigFileFolder = TestFilesystemUtility.CreateRandomTestFolder();
            var nugetConfigFilePath = Path.Combine(nugetConfigFileFolder, "nuget.config");
            File.Create(nugetConfigFilePath).Close();

            var enabledReplacement = @"<add key='anotherName' value='" + NuGetConstants.V3FeedUrl + "' />";
            enabledReplacement = enabledReplacement + @"<add key='" + NuGetConstants.FeedName + "' value='" + NuGetConstants.V2FeedUrl + "' />";
            var disabledReplacement = string.Empty;
            File.WriteAllText(nugetConfigFilePath, CreateNuGetConfigContent(enabledReplacement, disabledReplacement));

            Settings settings = new Settings(nugetConfigFileFolder, "nuget.config");
            PackageSourceProvider before = new PackageSourceProvider(settings);
            VerifyPackageSource(before, 2, new string[] { "anotherName", NuGetConstants.FeedName },
                new string[] { NuGetConstants.V3FeedUrl, NuGetConstants.V2FeedUrl },
                new bool[] { true, true }, new bool[] { false, false });

            List<PackageSource> primary = new List<PackageSource>();
            PackageSource item = new PackageSource(NuGetConstants.V3FeedUrl, NuGetConstants.FeedName);
            primary.Add(item);

            List<PackageSource> secondary = new List<PackageSource>();
            PackageSource item2 = new PackageSource(NuGetConstants.V2FeedUrl, NuGetConstants.FeedName, false);
            secondary.Add(item2);

            PackageSourceProvider after = new PackageSourceProvider(settings, primary, secondary);

            // Assert
            //Primary feed is present in nuget.config but with a different name
            //In this case, we don't set IsOfficial = true
            //Secondary matches both name and URL so secondary is set to true
            //Since this is not the first time primary is getting added, we aren't aggressive in demoting secondary from enabled to disabled
            VerifyPackageSource(after, 2, new string[] { "anotherName", NuGetConstants.FeedName },
                new string[] { NuGetConstants.V3FeedUrl, NuGetConstants.V2FeedUrl },
                new bool[] { true, true }, new bool[] { false, true });

            //Clean up
            TestFilesystemUtility.DeleteRandomTestFolders(nugetConfigFileFolder);
        }

        [Fact]
        public void SecondaryNameNotChangedWhenTheFeedHasAnotherName()
        {
            // Act
            //Create nuget.config that has Primary defined and Secondary missing
            string nugetConfigFileFolder = TestFilesystemUtility.CreateRandomTestFolder();
            var nugetConfigFilePath = Path.Combine(nugetConfigFileFolder, "nuget.config");
            File.Create(nugetConfigFilePath).Close();

            var enabledReplacement = @"<add key='" + NuGetConstants.FeedName + "' value='" + NuGetConstants.V3FeedUrl + "' />";
            enabledReplacement = enabledReplacement + @"<add key='anotherName' value='" + NuGetConstants.V2FeedUrl + "' />";
            var disabledReplacement = string.Empty;
            File.WriteAllText(nugetConfigFilePath, CreateNuGetConfigContent(enabledReplacement, disabledReplacement));

            Settings settings = new Settings(nugetConfigFileFolder, "nuget.config");
            PackageSourceProvider before = new PackageSourceProvider(settings);
            VerifyPackageSource(before, 2, new string[] { NuGetConstants.FeedName, "anotherName" },
                new string[] { NuGetConstants.V3FeedUrl, NuGetConstants.V2FeedUrl },
                new bool[] { true, true }, new bool[] { false, false });

            List<PackageSource> primary = new List<PackageSource>();
            PackageSource item = new PackageSource(NuGetConstants.V3FeedUrl, NuGetConstants.FeedName);
            primary.Add(item);

            List<PackageSource> secondary = new List<PackageSource>();
            PackageSource item2 = new PackageSource(NuGetConstants.V2FeedUrl, NuGetConstants.FeedName, false);
            secondary.Add(item2);

            PackageSourceProvider after = new PackageSourceProvider(settings, primary, secondary);

            // Assert
            //Secondary feed is present in nuget.config but with a different name
            //In this case, we don't set IsOfficial = true
            //Primary matches both name and URL so primary's IsOfficial is set to true
            //Since this is not the first time primary is getting added, we aren't aggressive in demoting secondary from enabled to disabled
            VerifyPackageSource(after, 2, new string[] { NuGetConstants.FeedName, "anotherName" },
                new string[] { NuGetConstants.V3FeedUrl, NuGetConstants.V2FeedUrl },
                new bool[] { true, true }, new bool[] { true, false });

            //Clean up
            TestFilesystemUtility.DeleteRandomTestFolders(nugetConfigFileFolder);
        }

        public void PrimaryIsEnabledAndSecondaryIsDisabledWhenPrimaryIsAddedForTheFirstTimeAndSecondaryAlreadyExists()
        {
            // Act
            //Create nuget.config that has Secondary defined
            string nugetConfigFileFolder = TestFilesystemUtility.CreateRandomTestFolder();
            var nugetConfigFilePath = Path.Combine(nugetConfigFileFolder, "nuget.Config");
            File.Create(nugetConfigFilePath).Close();

            var enabledReplacement = @"<add key='" + NuGetConstants.FeedName + "' value='" + NuGetConstants.V2FeedUrl + "' />";
            var disabledReplacement = string.Empty;
            File.WriteAllText(nugetConfigFilePath, CreateNuGetConfigContent(enabledReplacement, disabledReplacement));

            Settings settings = new Settings(nugetConfigFileFolder, "nuget.config");
            PackageSourceProvider before = new PackageSourceProvider(settings);
            VerifyPackageSource(before, 1, new string[] { NuGetConstants.FeedName },
                new string[] { NuGetConstants.V2FeedUrl },
                new bool[] { true }, new bool[] { false });

            List<PackageSource> primary = new List<PackageSource>();
            PackageSource item = new PackageSource(NuGetConstants.V3FeedUrl, NuGetConstants.FeedName);
            primary.Add(item);

            List<PackageSource> secondary = new List<PackageSource>();
            PackageSource item2 = new PackageSource(NuGetConstants.V2FeedUrl, NuGetConstants.FeedName, false);
            secondary.Add(item2);

            PackageSourceProvider after = new PackageSourceProvider(settings, primary, secondary);

            // Assert
            //First time Primary is getting added so it is set to Enabled
            //Secondary is demoted to disabled even though it is already enabled through nuget.config
            VerifyPackageSource(after, 2, new string[] { NuGetConstants.FeedName, NuGetConstants.FeedName },
                new string[] { NuGetConstants.V2FeedUrl, NuGetConstants.V3FeedUrl },
                new bool[] { false, true }, new bool[] { true, true });

            //Clean up
            TestFilesystemUtility.DeleteRandomTestFolders(nugetConfigFileFolder);
        }

        [Fact]
        public void ActivePackageSourceCanBeReadAndWrittenInNuGetConfig()
        {
            // Act
            //Create nuget.config that has active package source defined
            string nugetConfigFileFolder = TestFilesystemUtility.CreateRandomTestFolder();
            var nugetConfigFilePath = Path.Combine(nugetConfigFileFolder, "nuget.Config");
            File.Create(nugetConfigFilePath).Close();

            var enabledReplacement = @"<add key='" + NuGetConstants.FeedName + "' value='" + NuGetConstants.V2FeedUrl + "' />";
            var disabledReplacement = string.Empty;
            var activeReplacement = @"<add key='" + NuGetConstants.FeedName + "' value='" + NuGetConstants.V2FeedUrl + "' />";
            File.WriteAllText(nugetConfigFilePath, CreateNuGetConfigContent(enabledReplacement, disabledReplacement, activeReplacement));

            Settings settings = new Settings(nugetConfigFileFolder, "nuget.config");
            PackageSourceProvider before = new PackageSourceProvider(settings);
            Assert.Equal(NuGetConstants.FeedName, before.ActivePackageSourceName);

            before.SaveActivePackageSource(new PackageSource(NuGetConstants.V3FeedUrl, NuGetConstants.FeedName));
            Assert.Equal(NuGetConstants.FeedName, before.ActivePackageSourceName);

            //Clean up
            TestFilesystemUtility.DeleteRandomTestFolders(nugetConfigFileFolder);
        }

        [Fact]
        public void ActivePackageSourceReturnsNullIfNotSetInNuGetConfig()
        {
            // Act
            //Create nuget.config that has active package source defined
            string nugetConfigFileFolder = TestFilesystemUtility.CreateRandomTestFolder();
            var nugetConfigFilePath = Path.Combine(nugetConfigFileFolder, "nuget.Config");
            File.Create(nugetConfigFilePath).Close();

            var enabledReplacement = @"<add key='" + NuGetConstants.FeedName + "' value='" + NuGetConstants.V2FeedUrl + "' />";
            File.WriteAllText(nugetConfigFilePath, CreateNuGetConfigContent(enabledReplacement));

            Settings settings = new Settings(nugetConfigFileFolder, "nuget.config");
            PackageSourceProvider before = new PackageSourceProvider(settings);
            Assert.Null(before.ActivePackageSourceName);

            before.SaveActivePackageSource(new PackageSource(NuGetConstants.V3FeedUrl, NuGetConstants.FeedName));
            Assert.Equal(NuGetConstants.FeedName, before.ActivePackageSourceName);

            //Clean up
            TestFilesystemUtility.DeleteRandomTestFolders(nugetConfigFileFolder);
        }

        [Fact]
        public void ActivePackageSourceReturnsNullIfNotPresentInNuGetConfig()
        {
            // Act
            //Create nuget.config that has active package source defined
            string nugetConfigFileFolder = TestFilesystemUtility.CreateRandomTestFolder();
            var nugetConfigFilePath = Path.Combine(nugetConfigFileFolder, "nuget.Config");
            File.Create(nugetConfigFilePath).Close();

            var enabledReplacement = @"<add key='" + NuGetConstants.FeedName + "' value='" + NuGetConstants.V2FeedUrl + "' />";
            var fileContents = CreateNuGetConfigContent(enabledReplacement);
            fileContents = fileContents.Replace("<activePackageSource>", string.Empty);
            fileContents = fileContents.Replace("</activePackageSource>", string.Empty);
            File.WriteAllText(nugetConfigFilePath, fileContents);

            Settings settings = new Settings(nugetConfigFileFolder, "nuget.config");
            PackageSourceProvider before = new PackageSourceProvider(settings);
            Assert.Null(before.ActivePackageSourceName);

            SettingValue newActiveValue = new SettingValue(NuGetConstants.FeedName, NuGetConstants.V3FeedUrl, false);
            before.SaveActivePackageSource(new PackageSource(NuGetConstants.V3FeedUrl, NuGetConstants.FeedName));
            Assert.Equal(NuGetConstants.FeedName, before.ActivePackageSourceName);

            //Clean up
            TestFilesystemUtility.DeleteRandomTestFolders(nugetConfigFileFolder);
        }

        [Fact]
        public async Task LoadPackageSourcesWithCredentials()
        {
            // Arrange
            //Create nuget.config that has active package source defined
            string nugetConfigFileFolder = TestFilesystemUtility.CreateRandomTestFolder();
            var nugetConfigFilePath = Path.Combine(nugetConfigFileFolder, "nuget.Config");

            var configContent = @"<?xml version='1.0' encoding='utf-8'?>
<configuration>
  <apikeys>
    <add key='https://www.nuget.org' value='removed' />
    <add key='https://www.myget.org/F/somecompanyfeed-unstable/' value='removed' />
    <add key='https://www.myget.org/F/somecompanyfeed/' value='removed' />
    <add key='https://www.myget.org/F/somecompanyfeed-unstable/api/v2/package' value='removed' />
    <add key='https://www.myget.org/F/somecompanyfeed/api/v2/package' value='removed' />
    <add key='https://www.myget.org/F/somecompanyfeed-unstable/api/v2/' value='removed' />
    <add key='https://nuget.smbsrc.net/' value='removed' />
  </apikeys>
  <packageRestore>
    <add key='enabled' value='True' />
    <add key='automatic' value='True' />
  </packageRestore>
  <activePackageSource>
    <add key='NuGet.org' value='https://nuget.org/api/v2/' />
  </activePackageSource>
  <packageSources>
    <add key='CodeCrackerUnstable' value='https://www.myget.org/F/codecrackerbuild/api/v2' />
    <add key='CompanyFeedUnstable' value='https://www.myget.org/F/somecompanyfeed-unstable/api/v2/' />
    <add key='NuGet.org' value='https://nuget.org/api/v2/' />
    <add key='AspNetVNextStable' value='https://www.myget.org/F/aspnetmaster/api/v2' />
    <add key='AspNetVNextUnstable' value='https://www.myget.org/F/aspnetvnext/api/v2' />
    <add key='CompanyFeed' value='https://www.myget.org/F/somecompanyfeed/api/v2/' />
  </packageSources>
  <packageSourceCredentials>
    <CodeCrackerUnstable>
      <add key='Username' value='myusername' />
    </CodeCrackerUnstable>
    <AspNetVNextUnstable>
      <add key='Username' value='myusername' />
    </AspNetVNextUnstable>
    <AspNetVNextStable>
      <add key='Username' value='myusername' />
    </AspNetVNextStable>
    <NuGet.org>
      <add key='Username' value='myusername' />
    </NuGet.org>
    <CompanyFeedUnstable>
      <add key='Username' value='myusername' />
      <add key='ClearTextPassword' value='removed' />
    </CompanyFeedUnstable>
    <CompanyFeed>
      <add key='Username' value='myusername' />
      <add key='ClearTextPassword' value='removed' />
    </CompanyFeed>
  </packageSourceCredentials>
</configuration>";

            using (var stream = File.Create(nugetConfigFilePath))
            {
                var bytes = Encoding.UTF8.GetBytes(configContent);
                await stream.WriteAsync(bytes, 0, configContent.Length);
            }

            Settings settings = new Settings(nugetConfigFileFolder, "nuget.config");
            PackageSourceProvider psp = new PackageSourceProvider(settings);

            var sources = psp.LoadPackageSources().ToList();

            Assert.Equal(6, sources.Count);
            Assert.NotNull(sources[1].Password);
            Assert.True(String.Equals(sources[1].Password, "removed", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(sources[5].Password);
            Assert.True(String.Equals(sources[5].Password, "removed", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void TestNoPackageSourcesAreReturnedIfUserSettingsIsEmpty()
        {
            // Arrange
            var provider = CreatePackageSourceProvider();

            // Act
            var values = provider.LoadPackageSources().ToList();

            // Assert
            Assert.Equal(0, values.Count);
        }

        [Fact]
        public void LoadPackageSourcesReturnsEmptySequenceIfDefaultPrimaryPackageSourceIsNull()
        {
            // Arrange
            var settings = new Mock<ISettings>();
            var provider = CreatePackageSourceProvider(settings.Object, providerDefaultPrimarySources: null);

            // Act
            var values = provider.LoadPackageSources();

            // Assert
            Assert.False(values.Any());
        }

        [Fact]
        public void LoadPackageSourcesReturnsEmptySequenceIfDefaultPackageSourceIsEmpty()
        {
            // Arrange
            var settings = new Mock<ISettings>();
            var provider = CreatePackageSourceProvider(settings.Object, providerDefaultPrimarySources: new PackageSource[] { });

            // Act
            var values = provider.LoadPackageSources();

            // Assert
            Assert.False(values.Any());
        }

        [Fact]
        public void LoadPackageSourcesReturnsDefaultSourcesIfSpecified()
        {
            // Arrange
            var settings = new Mock<ISettings>().Object;
            var provider = CreatePackageSourceProvider(settings, providerDefaultPrimarySources: new[] { new PackageSource("A"), new PackageSource("B") });

            // Act
            var values = provider.LoadPackageSources().ToList();

            // Assert
            Assert.Equal(2, values.Count);
            Assert.Equal("A", values.First().Source);
            Assert.Equal("B", values.Last().Source);
        }

        [Fact]
        public void LoadPackageSourcesWhereAMigratedSourceIsAlsoADefaultSource()
        {
            // Arrange
            var settings = new Mock<ISettings>();
            settings.Setup(s => s.GetSettingValues("packageSources", true))
                .Returns(new[] { new SettingValue("AOld", "urlA", false), new SettingValue("userDefinedSource", "userDefinedSourceUrl", false) });
            settings.Setup(s => s.GetSettingValues("disabledPackageSources", false)).Returns(new SettingValue[0]);
            settings.Setup(s => s.GetNestedValues("packageSourceCredentials", It.IsAny<string>())).Returns(new KeyValuePair<string, string>[0]);

            var defaultPackageSourceA = new PackageSource("urlA", "ANew");
            var defaultPackageSourceB = new PackageSource("urlB", "B");

            var provider = CreatePackageSourceProvider(settings.Object, providerDefaultPrimarySources: new[] { defaultPackageSourceA, defaultPackageSourceB },
                migratePackageSources: new Dictionary<PackageSource, PackageSource>
                    {
                        { new PackageSource("urlA", "AOld"), defaultPackageSourceA },
                    });

            // Act
            var values = provider.LoadPackageSources().ToList();

            // Assert
            // Package Source AOld will be migrated to ANew. B will simply get added
            // Since default source B got added when there are other package sources it will be disabled
            // However, package source ANew must stay enabled
            // PackageSource userDefinedSource is a user package source and is untouched
            Assert.Equal(3, values.Count);
            Assert.Equal("urlA", values[0].Source);
            Assert.Equal("ANew", values[0].Name);
            Assert.True(values[0].IsEnabled);
            Assert.Equal("userDefinedSourceUrl", values[1].Source);
            Assert.Equal("userDefinedSource", values[1].Name);
            Assert.True(values[1].IsEnabled);
            Assert.Equal("urlB", values[2].Source);
            Assert.Equal("B", values[2].Name);
            Assert.False(values[2].IsEnabled);
        }

        [Fact]
        public void LoadPackageSourcesPerformMigrationIfSpecified()
        {
            // Arrange
            var settings = new Mock<ISettings>();
            settings.Setup(s => s.GetSettingValues("packageSources", true)).Returns(
                new[]
                    {
                        new SettingValue("one", "onesource", false),
                        new SettingValue("two", "twosource", false),
                        new SettingValue("three", "threesource", false),
                    }
                );

            // disable package "three"
            settings.Setup(s => s.GetSettingValues("disabledPackageSources", false)).Returns(
                new[] { new SettingValue("three", "true", false) });

            IReadOnlyList<SettingValue> savedSettingValues = null;
            settings.Setup(s => s.UpdateSections("packageSources", It.IsAny<IReadOnlyList<SettingValue>>()))
                .Callback<string, IReadOnlyList<SettingValue>>((_, savedVals) => { savedSettingValues = savedVals; })
                .Verifiable();

            var provider = CreatePackageSourceProvider(settings.Object,
                null,
                null,
                new Dictionary<PackageSource, PackageSource>
                    {
                        { new PackageSource("onesource", "one"), new PackageSource("goodsource", "good") },
                        { new PackageSource("foo", "bar"), new PackageSource("foo", "bar") },
                        { new PackageSource("threesource", "three"), new PackageSource("awesomesource", "awesome") }
                    }
                );

            // Act
            var values = provider.LoadPackageSources().ToList();
            savedSettingValues = savedSettingValues.ToList();

            // Assert
            Assert.Equal(3, values.Count);
            AssertPackageSource(values[0], "good", "goodsource", true);
            AssertPackageSource(values[1], "two", "twosource", true);
            AssertPackageSource(values[2], "awesome", "awesomesource", false);

            Assert.Equal(3, savedSettingValues.Count);
            Assert.Equal("good", savedSettingValues[0].Key);
            Assert.Equal("goodsource", savedSettingValues[0].Value);
            Assert.Equal("two", savedSettingValues[1].Key);
            Assert.Equal("twosource", savedSettingValues[1].Value);
            Assert.Equal("awesome", savedSettingValues[2].Key);
            Assert.Equal("awesomesource", savedSettingValues[2].Value);
        }

        public void SavePackageSourcesTest()
        {
            // Arrange
            var settings = Settings.LoadDefaultSettings(
                Directory.GetCurrentDirectory(),
                configFileName: null,
                machineWideSettings: null);

            var provider = CreatePackageSourceProvider(settings);

            // Act            
            provider.SavePackageSources(
                new PackageSource[]
                    {
                        new PackageSource("http://a", "a")
                            {
                                IsEnabled = true
                            },
                        new PackageSource("http://b", "b")
                            {
                                IsEnabled = false
                            },
                        new PackageSource("http://c", "c", isEnabled: true, isOfficial: false, isPersistable: false),
                        new PackageSource("http://d", "d", isEnabled: false, isOfficial: false, isPersistable: false),
                    });

            // Assert:
            // - source a is persisted in <packageSources>
            // - source b is persisted in <packageSources> and <disabledPackageSources>
            // - source c is not spersisted at all since its IsPersistable is false and it's enabled.
            // - source d is persisted in <disabledPackageSources> only since its IsPersistable is false and it's disabled.
#if !DNXCORE50
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
#else
            string appDataPath = Environment.GetEnvironmentVariable("AppData");
#endif
            var configFileContent = File.ReadAllText(Path.Combine(appDataPath, @"NuGet\NuGet.config"));
            var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""a"" value=""http://a"" />
    <add key=""b"" value=""http://b"" />
  </packageSources>
  <disabledPackageSources>
    <add key=""b"" value=""true"" />
    <add key=""d"" value=""true"" />
  </disabledPackageSources>
</configuration>".Replace("\r\n", "\n");

            Assert.Equal(result, configFileContent.Replace("\r\n", "\n"));
        }

        [Fact]
        public void SavePackageSourcesWithRelativePath()
        {
            using (var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var configContents =
                    @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""nuget.org"" value=""https://nuget.org"" />
        <add key=""test.org"" value=""Packages"" /> 
    </packageSources>
</configuration>
";
                File.WriteAllText(Path.Combine(mockBaseDirectory.Path, "NuGet.config"), configContents);

                var rootPath = Path.Combine(mockBaseDirectory.Path, Path.GetRandomFileName());
                
                var settings = Settings.LoadDefaultSettings(rootPath,
                    configFileName: null,
                    machineWideSettings: null,
                    loadAppDataSettings: false);
                var packageSourceProvider = new PackageSourceProvider(settings);
                var packageSourceList = packageSourceProvider.LoadPackageSources().ToList();
                
                // Act
                packageSourceProvider.SavePackageSources(packageSourceList);

                // Assert
                Assert.Equal(
                       @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""nuget.org"" value=""https://nuget.org"" />
        <add key=""test.org"" value=""Packages"" />
    </packageSources>
</configuration>
".Replace("\r\n", "\n"),
                   File.ReadAllText(Path.Combine(mockBaseDirectory.Path, "NuGet.config")).Replace("\r\n", "\n"));
            }
        }

        [Fact]
        public void SavePackageSourcesWithRelativePathAndAddNewSource()
        {
            using (var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var configContents =
                    @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""nuget.org"" value=""https://nuget.org"" />
        <add key=""test.org"" value=""Packages"" /> 
    </packageSources>
</configuration>
";
                File.WriteAllText(Path.Combine(mockBaseDirectory.Path, "NuGet.config"), configContents);

                var rootPath = Path.Combine(mockBaseDirectory.Path, Path.GetRandomFileName());

                var settings = Settings.LoadDefaultSettings(rootPath,
                    configFileName: null,
                    machineWideSettings: null,
                    loadAppDataSettings: false);
                var packageSourceProvider = new PackageSourceProvider(settings);
                var packageSourceList = packageSourceProvider.LoadPackageSources().ToList();

                // Act
                packageSourceList.Add(new PackageSource("https://test3.net", "test3"));
                packageSourceProvider.SavePackageSources(packageSourceList);

                // Assert
                Assert.Equal(
                       @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""nuget.org"" value=""https://nuget.org"" />
        <add key=""test.org"" value=""Packages"" />
        <add key=""test3"" value=""https://test3.net"" />
    </packageSources>
</configuration>
".Replace("\r\n", "\n"),
                   File.ReadAllText(Path.Combine(mockBaseDirectory.Path, "NuGet.config")).Replace("\r\n", "\n"));
            }
        }

        [Fact]
        public void SavePackageSourcesWithOneClear()
        {
            using (var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var configContents =
                    @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""nuget.org"" value=""https://nuget.org"" />
        <add key=""test.org"" value=""https://test.org"" /> 
         <clear /> 
        <add key=""test.org"" value=""https://new.test.org"" protocolVersion=""3"" />
        <add key=""test2"" value=""https://test2.net"" />
    </packageSources>
</configuration>
";
                File.WriteAllText(Path.Combine(mockBaseDirectory.Path, "NuGet.config"), configContents);

                var rootPath = Path.Combine(mockBaseDirectory.Path, Path.GetRandomFileName());

                var settings = Settings.LoadDefaultSettings(rootPath,
                    configFileName: null,
                    machineWideSettings: null,
                    loadAppDataSettings: false);
                var packageSourceProvider = new PackageSourceProvider(settings);
                var packageSourceList = packageSourceProvider.LoadPackageSources().ToList();

                // act
                packageSourceList.Add(new PackageSource("https://test3.net", "test3"));
                packageSourceProvider.SavePackageSources(packageSourceList);

                // Assert
                Assert.Equal(
                       @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""nuget.org"" value=""https://nuget.org"" />
        <add key=""test.org"" value=""https://test.org"" /> 
         <clear />
        <add key=""test.org"" value=""https://new.test.org"" protocolVersion=""3"" />
        <add key=""test2"" value=""https://test2.net"" />
        <add key=""test3"" value=""https://test3.net"" />
    </packageSources>
</configuration>
".Replace("\r\n", "\n"),
                   File.ReadAllText(Path.Combine(mockBaseDirectory.Path, "NuGet.config")).Replace("\r\n", "\n"));
            }
        }

        [Fact]
        public void SavePackageSourcesWithMoreCLear()
        {
            using (var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var configContents =
                    @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""nuget.org"" value=""https://nuget.org"" />
        <add key=""test.org"" value=""https://test.org"" /> 
        <clear /> 
        <add key=""test.org"" value=""https://new.test.org"" protocolVersion=""3"" />
        <clear />
        <add key=""test2"" value=""https://test2.net"" />
    </packageSources>
</configuration>
";
                File.WriteAllText(Path.Combine(mockBaseDirectory.Path, "NuGet.config"), configContents);

                var rootPath = Path.Combine(mockBaseDirectory.Path, Path.GetRandomFileName());

                var settings = Settings.LoadDefaultSettings(rootPath,
                    configFileName: null,
                    machineWideSettings: null,
                    loadAppDataSettings: false);
                var packageSourceProvider = new PackageSourceProvider(settings);
                var packageSourceList = packageSourceProvider.LoadPackageSources().ToList();

                // act
                packageSourceList.Add(new PackageSource("https://test3.net", "test3"));
                packageSourceProvider.SavePackageSources(packageSourceList);

                // Assert
                Assert.Equal(
                       @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""nuget.org"" value=""https://nuget.org"" />
        <add key=""test.org"" value=""https://test.org"" /> 
        <clear /> 
        <add key=""test.org"" value=""https://new.test.org"" protocolVersion=""3"" />
        <clear />
        <add key=""test2"" value=""https://test2.net"" />
        <add key=""test3"" value=""https://test3.net"" />
    </packageSources>
</configuration>
".Replace("\r\n", "\n"),
                   File.ReadAllText(Path.Combine(mockBaseDirectory.Path, "NuGet.config")).Replace("\r\n", "\n"));
            }
        }

        [Fact]
        public void SavePackageSourcesWithOnlyClear()
        {
            using (var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var configContents =
                    @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <clear /> 
    </packageSources>
    <disabledPackageSources>
        <clear />
    </disabledPackageSources>
</configuration>
";
                File.WriteAllText(Path.Combine(mockBaseDirectory.Path, "NuGet.config"), configContents);

                var rootPath = Path.Combine(mockBaseDirectory.Path, Path.GetRandomFileName());

                var settings = Settings.LoadDefaultSettings(rootPath,
                    configFileName: null,
                    machineWideSettings: null,
                    loadAppDataSettings: false);
                var packageSourceProvider = new PackageSourceProvider(settings);
                var packageSourceList = packageSourceProvider.LoadPackageSources().ToList();

                // act
                packageSourceList.Add(new PackageSource("https://test3.net", "test3"));
                packageSourceProvider.SavePackageSources(packageSourceList);

                // Assert
                Assert.Equal(
                       @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <clear /> 
        <add key=""test3"" value=""https://test3.net"" />
    </packageSources>
    <disabledPackageSources>
        <clear />
    </disabledPackageSources>
</configuration>
".Replace("\r\n", "\n"),
                   File.ReadAllText(Path.Combine(mockBaseDirectory.Path, "NuGet.config")).Replace("\r\n", "\n"));
            }
        }

        [Fact]
        public void SavePackageSourcesWithHierarchyClear()
        {
            using (var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder())
            {
                // assert
                var nugetConfigPath = "NuGet.Config";
                var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <!-- i.e. ignore values from prior conf files -->
    <clear />
    <add key=""key2"" value=""https://test.org/2"" />
  </packageSources>
</configuration>";
                TestFilesystemUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, @"dir1\dir2"), config);
                config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""key1"" value=""https://test.org/1"" /> 
    <clear />   
  </packageSources>
</configuration>";
                TestFilesystemUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, "dir1"), config);

                var rootPath = Path.Combine(Path.Combine(mockBaseDirectory, @"dir1\dir2"), Path.GetRandomFileName());
                var settings = Settings.LoadDefaultSettings(rootPath,
                    configFileName: null,
                    machineWideSettings: null,
                    loadAppDataSettings: false);

                var packageSourceProvider = new PackageSourceProvider(settings);
                var packageSourceList = packageSourceProvider.LoadPackageSources().ToList();

                // act
                packageSourceList.Add(new PackageSource("https://test3.net", "test3"));
                packageSourceProvider.SavePackageSources(packageSourceList);

                // Assert
                Assert.Equal(
                       @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <!-- i.e. ignore values from prior conf files -->
    <clear />
    <add key=""key2"" value=""https://test.org/2"" />
    <add key=""test3"" value=""https://test3.net"" />
  </packageSources>
</configuration>".Replace("\r\n", "\n"),
                   File.ReadAllText(Path.Combine(mockBaseDirectory.Path, @"dir1\dir2\NuGet.config")).Replace("\r\n", "\n"));

                Assert.Equal(
                     @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""key1"" value=""https://test.org/1"" /> 
    <clear />   
  </packageSources>
</configuration>".Replace("\r\n", "\n"),
                  File.ReadAllText(Path.Combine(mockBaseDirectory.Path, @"dir1\NuGet.config")).Replace("\r\n", "\n"));
            }
        }

        [Fact]
        public void SavePackageSources_RetainUnavailableDisabledSources()
        {
            using (var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var configContents =
                    @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""a"" value=""https://a"" />
        <add key=""b"" value=""https://b"" />
    </packageSources>
    <disabledPackageSources>
        <add key=""Microsoft and .NET"" value=""true"" />
        <add key=""b"" value=""true"" />
    </disabledPackageSources>
</configuration>
";
                File.WriteAllText(Path.Combine(mockBaseDirectory.Path, "NuGet.config"), configContents);

                var rootPath = Path.Combine(mockBaseDirectory.Path, Path.GetRandomFileName());

                var settings = Settings.LoadDefaultSettings(rootPath,
                    configFileName: null,
                    machineWideSettings: null,
                    loadAppDataSettings: false);

                var expectedDisabledSources = settings.GetSettingValues("disabledPackageSources")?.ToList();

                var packageSourceProvider = new PackageSourceProvider(settings);
                var packageSourceList = packageSourceProvider.LoadPackageSources().ToList();

                // Act
                packageSourceProvider.SavePackageSources(packageSourceList);

                var newSettings = Settings.LoadDefaultSettings(rootPath,
                    configFileName: null,
                    machineWideSettings: null,
                    loadAppDataSettings: false);

                var actualDisabledSources = newSettings.GetSettingValues("disabledPackageSources").ToList();

                Assert.Equal(expectedDisabledSources.Count, actualDisabledSources.Count);
                foreach(var item in expectedDisabledSources)
                {
                    Assert.Contains(item, actualDisabledSources);
                }
            }
        }

        [Fact]
        public void SavePackageSources_EnablesDisabledSources()
        {
            using (var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var configContents =
                    @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""a"" value=""https://a"" />
        <add key=""b"" value=""https://b"" />
    </packageSources>
    <disabledPackageSources>
        <add key=""Microsoft and .NET"" value=""true"" />
        <add key=""b"" value=""true"" />
    </disabledPackageSources>
</configuration>
";
                File.WriteAllText(Path.Combine(mockBaseDirectory.Path, "NuGet.config"), configContents);

                var rootPath = Path.Combine(mockBaseDirectory.Path, Path.GetRandomFileName());

                var settings = Settings.LoadDefaultSettings(rootPath,
                    configFileName: null,
                    machineWideSettings: null,
                    loadAppDataSettings: false);

                var disabledSources = settings.GetSettingValues("disabledPackageSources")?.ToList();

                // Pre-Assert
                Assert.Equal(2, disabledSources.Count);
                Assert.Equal("Microsoft and .NET", disabledSources[0].Key);
                Assert.Equal("b", disabledSources[1].Key);

                var packageSourceProvider = new PackageSourceProvider(settings);
                var packageSourceList = packageSourceProvider.LoadPackageSources().ToList();

                Assert.Equal(2, packageSourceList.Count);
                Assert.Equal("a", packageSourceList[0].Name);
                Assert.True(packageSourceList[0].IsEnabled);
                Assert.Equal("b", packageSourceList[1].Name);
                Assert.False(packageSourceList[1].IsEnabled);

                // Main Act
                packageSourceList[1].IsEnabled = true;
                packageSourceProvider.SavePackageSources(packageSourceList);

                var newSettings = Settings.LoadDefaultSettings(rootPath,
                    configFileName: null,
                    machineWideSettings: null,
                    loadAppDataSettings: false);

                // Main Assert
                disabledSources = newSettings.GetSettingValues("disabledPackageSources")?.ToList();

                Assert.Equal(1, disabledSources.Count);
                Assert.Equal("Microsoft and .NET", disabledSources[0].Key);

                packageSourceProvider = new PackageSourceProvider(newSettings);
                packageSourceList = packageSourceProvider.LoadPackageSources().ToList();

                Assert.Equal(2, packageSourceList.Count);
                Assert.Equal("a", packageSourceList[0].Name);
                Assert.True(packageSourceList[0].IsEnabled);
                Assert.Equal("b", packageSourceList[1].Name);
                Assert.True(packageSourceList[1].IsEnabled);
            }
        }

        [Fact]
        public void WithMachineWideSources()
        {
            // Arrange           
            var settings = new Mock<ISettings>();
            settings.Setup(s => s.GetSettingValues("packageSources", true))
                .Returns(new[]
                    {
                        new SettingValue("one", "one", true),
                        new SettingValue("two", "two", false, 2),
                        new SettingValue("three", "three", false, 2)
                    });

            settings.Setup(s => s.SetValues("packageSources", It.IsAny<IReadOnlyList<SettingValue>>()))
                .Callback((string section, IReadOnlyList<SettingValue> values) =>
                    {
                        // verifies that only sources "two" and "three" are passed.
                        // the machine wide source "one" is not.
                        Assert.Equal(2, values.Count);
                        Assert.Equal("two", values[0].Key);
                        Assert.Equal("two", values[0].Value);
                        Assert.Equal("three", values[1].Key);
                        Assert.Equal("three", values[1].Value);
                    })
                .Verifiable();

            settings.Setup(s => s.SetValues("disabledPackageSources", It.IsAny<IReadOnlyList<SettingValue>>()))
                .Callback((string section, IReadOnlyList<SettingValue> values) =>
                    {
                        // verifies that the machine wide source "one" is passed here
                        // since it is disabled.                    
                        Assert.Equal(1, values.Count);
                        Assert.Equal("one", values[0].Key);
                        Assert.Equal("true", values[0].Value);
                    })
                .Verifiable();

            var provider = CreatePackageSourceProvider(settings.Object);

            // Act
            var sources = provider.LoadPackageSources().ToList();

            // disable the machine wide source "one", and save the result in provider.
            Assert.Equal("one", sources[2].Name);
            sources[2].IsEnabled = false;
            provider.SavePackageSources(sources);

            // Assert
            // all assertions are done inside Callback()'s
        }

        [Fact]
        public void LoadPackageSourcesProvidesOriginData()
        {
            // Arrange
            var otherSettings = new Mock<ISettings>(MockBehavior.Strict);
            var settings = new Mock<ISettings>(MockBehavior.Strict);
            settings.Setup(s => s.GetSettingValues("packageSources", true))
                .Returns(new[]
                    {
                        new SettingValue("one", "onesource", origin: settings.Object, isMachineWide: true),
                        new SettingValue("two", "twosource", origin: settings.Object, isMachineWide: false, priority: 1),
                        new SettingValue("one", "newonesource", origin: otherSettings.Object, isMachineWide: false, priority: 1)
                    })
                .Verifiable();

            settings.Setup(s => s.GetSettingValues("disabledPackageSources", false)).Returns(
                new SettingValue[0]);
            settings.Setup(s => s.GetNestedValues("packageSourceCredentials", It.IsAny<string>())).Returns(new KeyValuePair<string, string>[0]);

            var provider = CreatePackageSourceProvider(settings.Object);

            // Act
            var values = provider.LoadPackageSources().ToList();

            // Assert
            Assert.Equal(2, values.Count);
            var one = values.Single(s => s.Name.Equals("one"));
            var two = values.Single(s => s.Name.Equals("two"));
            Assert.Same(otherSettings.Object, one.Origin);
            Assert.Same(settings.Object, two.Origin);
        }

        [Fact]
        public void LoadPackageSourcesReturnCorrectDataFromSettings()
        {
            // Arrange
            var settings = new Mock<ISettings>(MockBehavior.Strict);
            settings.Setup(s => s.GetSettingValues("packageSources", true))
                .Returns(new[]
                    {
                        new SettingValue("one", "onesource", true),
                        new SettingValue("two", "twosource", false, 1),
                        new SettingValue("three", "threesource", false, 1)
                    })
                .Verifiable();

            settings.Setup(s => s.GetSettingValues("disabledPackageSources", false)).Returns(
                new SettingValue[0]);
            settings.Setup(s => s.GetNestedValues("packageSourceCredentials", It.IsAny<string>())).Returns(new KeyValuePair<string, string>[0]);

            var provider = CreatePackageSourceProvider(settings.Object);

            // Act
            var values = provider.LoadPackageSources().ToList();

            // Assert
            Assert.Equal(3, values.Count);
            AssertPackageSource(values[0], "two", "twosource", true);
            AssertPackageSource(values[1], "three", "threesource", true);
            AssertPackageSource(values[2], "one", "onesource", true, true);
        }

        [Fact]
        public void LoadPackageSourcesReturnCorrectDataFromSettingsWhenSomePackageSourceIsDisabled()
        {
            // Arrange
            var settings = new Mock<ISettings>(MockBehavior.Strict);
            settings.Setup(s => s.GetSettingValues("packageSources", true))
                .Returns(new[]
                    {
                        new SettingValue("one", "onesource", false),
                        new SettingValue("two", "twosource", false),
                        new SettingValue("three", "threesource", false)
                    });

            settings.Setup(s => s.GetSettingValues("disabledPackageSources", false)).Returns(
                new[] { new SettingValue("two", "true", false) });
            settings.Setup(s => s.GetNestedValues("packageSourceCredentials", It.IsAny<string>())).Returns(new KeyValuePair<string, string>[0]);

            var provider = CreatePackageSourceProvider(settings.Object);

            // Act
            var values = provider.LoadPackageSources().ToList();

            // Assert
            Assert.Equal(3, values.Count);
            AssertPackageSource(values[0], "one", "onesource", true);
            AssertPackageSource(values[1], "two", "twosource", false);
            AssertPackageSource(values[2], "three", "threesource", true);
        }

        [Fact]
        public void LoadPackageSourcesDoesNotDuplicateFeedsOnMigration()
        {
            // Arrange
            var settings = new Mock<ISettings>();
            settings.Setup(s => s.GetSettingValues("packageSources", true))
                .Returns(new List<SettingValue>()
                    {
                        new SettingValue("NuGet official package source", "https://nuget.org/api/v2", false),
                        new SettingValue("nuget.org", "https://www.nuget.org/api/v2", false)
                    });
            settings.Setup(s => s.GetNestedValues("packageSourceCredentials", It.IsAny<string>())).Returns(new KeyValuePair<string, string>[0]);
            settings.Setup(s => s.GetSettingValues("disabledPackageSources", false)).Returns(new SettingValue[0]);

            var provider = CreatePackageSourceProvider(settings.Object, providerDefaultPrimarySources: null,
                providerDefaultSecondarySources: null,
                migratePackageSources: new Dictionary<PackageSource, PackageSource>
                    {
                        { new PackageSource("https://nuget.org/api/v2", "NuGet official package source"), new PackageSource("https://www.nuget.org/api/v2", "nuget.org") }
                    });

            // Act
            var values = provider.LoadPackageSources().ToList();

            // Assert
            Assert.Equal(1, values.Count);
            Assert.Equal("nuget.org", values[0].Name);
            Assert.Equal("https://www.nuget.org/api/v2", values[0].Source);
        }

        [Fact]
        public void LoadPackageSources_ReadsSourcesWithProtocolVersionFromPackageSourceSections()
        {
            // Arrange
            var settings = new Mock<ISettings>();
            var settingWithV3Protocol1 = new SettingValue("Source2", "https://source-with-newer-protocol", false);
            settingWithV3Protocol1.AdditionalData["protocolVersion"] = "3";

            var settingWithV3Protocol2 = new SettingValue("Source3", "Source3", false);
            settingWithV3Protocol2.AdditionalData["protocolVersion"] = "3";

            var sourcesSettings = new[]
                {
                    new SettingValue("Source1", "https://some-source.org", false),
                    settingWithV3Protocol1,
                    settingWithV3Protocol2,
                    new SettingValue("Source3", "Source3", false),
                    new SettingValue("Source4", "//Source4", false)
                };

            settings.Setup(s => s.GetSettingValues("packageSources", true))
                .Returns(sourcesSettings);

            settings.Setup(s => s.GetNestedValues("packageSourceCredentials", "Source3"))
                .Returns(new[]
                    {
                        new KeyValuePair<string, string>("Username", "source3-user"),
                        new KeyValuePair<string, string>("ClearTextPassword", "source3-password"),
                    });
            settings.Setup(s => s.GetSettingValues("disabledPackageSources", false))
                .Returns(new[]
                    {
                        new SettingValue("Source4", "true", isMachineWide: false)
                    });

            var provider = CreatePackageSourceProvider(settings.Object,
                providerDefaultPrimarySources: null,
                providerDefaultSecondarySources: null,
                migratePackageSources: null);

            // Act
            var values = provider.LoadPackageSources().ToList();

            // Assert
            Assert.Collection(values,
                source =>
                    {
                        Assert.Equal("Source1", source.Name);
                        Assert.Equal("https://some-source.org", source.Source);
                        Assert.Null(source.UserName);
                        Assert.Null(source.Password);
                        Assert.Equal(2, source.ProtocolVersion);
                        Assert.True(source.IsEnabled);
                    },
                source =>
                    {
                        Assert.Equal("Source2", source.Name);
                        Assert.Equal("https://source-with-newer-protocol", source.Source);
                        Assert.Null(source.UserName);
                        Assert.Null(source.Password);
                        Assert.Equal(3, source.ProtocolVersion);
                        Assert.True(source.IsEnabled);
                    },
                source =>
                    {
                        Assert.Equal("Source3", source.Name);
                        Assert.Equal("Source3", source.Source);
                        Assert.Equal("source3-user", source.UserName);
                        Assert.Equal("source3-password", source.Password);
                        Assert.Equal(3, source.ProtocolVersion);
                        Assert.True(source.IsEnabled);
                    },
                source =>
                    {
                        Assert.Equal("Source4", source.Name);
                        Assert.Equal("//Source4", source.Source);
                        Assert.Null(source.UserName);
                        Assert.Null(source.Password);
                        Assert.Equal(2, source.ProtocolVersion);
                        Assert.False(source.IsEnabled);
                    });
        }

        [Fact]
        public void LoadPackageSources_MigratesSourcesToNewerProtocol_ButKeepsExistingSourcesInPackageSourcesElement()
        {
            // Arrange
            var settings = new Mock<ISettings>();
            settings.Setup(s => s.GetSettingValues("packageSources", true))
                .Returns(new[]
                    {
                        new SettingValue("Source1", "https://some-source.org", false),
                        new SettingValue("Source2", "https://source-with-older-protocol", false)
                    });

            settings.Setup(s => s.GetNestedValues("packageSourceCredentials", "Source2"))
                .Returns(new[]
                    {
                        new KeyValuePair<string, string>("Username", "Source2-user"),
                        new KeyValuePair<string, string>("ClearTextPassword", "Source2-password"),
                    });

            settings.Setup(s => s.UpdateSections("packageSources", It.IsAny<IReadOnlyList<SettingValue>>()))
                .Callback((string source, IReadOnlyList<SettingValue> setValues) =>
                    {
                        Assert.Collection(setValues,
                            value =>
                                {
                                    Assert.Equal("Source1", value.Key);
                                    Assert.Equal("https://some-source.org", value.Value);
                                },
                            value =>
                                {
                                    Assert.Equal("Source2", value.Key);
                                    Assert.Equal("https://source-with-older-protocol", value.Value);
                                },
                            value =>
                                {
                                    Assert.Equal("Source2", value.Key);
                                    Assert.Equal("https://api.source-with-newer-protocol/v3/", value.Value);
                                    Assert.Collection(value.AdditionalData,
                                        item =>
                                            {
                                                Assert.Equal("protocolVersion", item.Key);
                                                Assert.Equal("3", item.Value);
                                            });
                                });
                    })
                .Verifiable();
           
            var migratePackageSources = new Dictionary<PackageSource, PackageSource>
                {
                    {
                        new PackageSource("https://source-with-older-protocol", "Source2"),
                        new PackageSource("https://api.source-with-newer-protocol/v3/", "Source2") { ProtocolVersion = 3 }
                    }
                };

            var provider = CreatePackageSourceProvider(settings.Object,
                providerDefaultPrimarySources: null,
                providerDefaultSecondarySources: null,
                migratePackageSources: migratePackageSources);

            // Act
            var values = provider.LoadPackageSources();

            // Assert
            Assert.Collection(values,
                source =>
                    {
                        Assert.Equal("Source1", source.Name);
                        Assert.Equal("https://some-source.org", source.Source);
                        Assert.Null(source.UserName);
                        Assert.Null(source.Password);
                        Assert.Equal(2, source.ProtocolVersion);
                        Assert.True(source.IsEnabled);
                    },
                source =>
                    {
                        Assert.Equal("Source2", source.Name);
                        Assert.Equal("https://api.source-with-newer-protocol/v3/", source.Source);
                        Assert.Null(source.UserName);
                        Assert.Null(source.Password);
                        Assert.Equal(3, source.ProtocolVersion);
                        Assert.True(source.IsEnabled);
                    });
            // Verify the sources got migrated.
            settings.Verify();
        }

        [Fact]
        public void LoadPackageSourcesDoesNotDuplicateFeedsOnMigrationAndSavesIt()
        {
            // Arrange
            var settings = new Mock<ISettings>();
            settings.Setup(s => s.GetSettingValues("packageSources", true))
                .Returns(new List<SettingValue>()
                    {
                        new SettingValue("NuGet official package source", "https://nuget.org/api/v2", false),
                        new SettingValue("nuget.org", "https://www.nuget.org/api/v2", false)
                    });
            settings.Setup(s => s.GetNestedValues("packageSourceCredentials", It.IsAny<string>())).Returns(new KeyValuePair<string, string>[0]);
            settings.Setup(s => s.GetSettingValues("disabledPackageSources", false)).Returns(new SettingValue[0]);

            settings.Setup(s => s.UpdateSections("packageSources", It.IsAny<IReadOnlyList<SettingValue>>()))
                .Callback((string section, IReadOnlyList<SettingValue> valuePairs) =>
                    {
                        var value = Assert.Single(valuePairs);
                        Assert.Equal("nuget.org", value.Key);
                        Assert.Equal("https://www.nuget.org/api/v2", value.Value);
                    })
                .Verifiable();

            settings.Setup(s => s.UpdateSections("disabledPackageSources", It.IsAny<IReadOnlyList<SettingValue>>()))
                .Callback((string section, IReadOnlyList<SettingValue> settingValues) => { Assert.Empty(settingValues); })
                .Verifiable();

            var provider = CreatePackageSourceProvider(settings.Object, providerDefaultPrimarySources: null,
                providerDefaultSecondarySources: null,
                migratePackageSources: new Dictionary<PackageSource, PackageSource>
                    {
                        { new PackageSource("https://nuget.org/api/v2", "NuGet official package source"), new PackageSource("https://www.nuget.org/api/v2", "nuget.org") }
                    });

            // Act
            var values = provider.LoadPackageSources().ToList();

            // Assert
            Assert.Equal(1, values.Count);
            Assert.Equal("nuget.org", values[0].Name);
            Assert.Equal("https://www.nuget.org/api/v2", values[0].Source);
            settings.Verify();
        }

        [Fact]
        public void DisablePackageSourceAddEntryToSettings()
        {
            // Arrange
            var settings = new Mock<ISettings>(MockBehavior.Strict);
            settings.Setup(s => s.SetValue("disabledPackageSources", "A", "true")).Verifiable();
            var provider = CreatePackageSourceProvider(settings.Object);

            // Act
            provider.DisablePackageSource(new PackageSource("source", "A"));

            // Assert
            settings.Verify();
        }

        [Fact]
        public void IsPackageSourceEnabledReturnsFalseIfTheSourceIsDisabled()
        {
            // Arrange
            var settings = new Mock<ISettings>(MockBehavior.Strict);
            settings.Setup(s => s.GetValue("disabledPackageSources", "A", false)).Returns("sdfds");
            var provider = CreatePackageSourceProvider(settings.Object);

            // Act
            bool isEnabled = provider.IsPackageSourceEnabled(new PackageSource("source", "A"));

            // Assert
            Assert.False(isEnabled);
        }

        [Theory]
        [InlineData((string)null)]
        [InlineData("")]
        public void IsPackageSourceEnabledReturnsTrueIfTheSourceIsNotDisabled(string returnValue)
        {
            // Arrange
            var settings = new Mock<ISettings>(MockBehavior.Strict);
            settings.Setup(s => s.GetValue("disabledPackageSources", "A", false)).Returns(returnValue);
            var provider = CreatePackageSourceProvider(settings.Object);

            // Act
            bool isEnabled = provider.IsPackageSourceEnabled(new PackageSource("source", "A"));

            // Assert
            Assert.True(isEnabled);
        }

        [Theory]
        [InlineData(new object[] { null, "abcd" })]
        [InlineData(new object[] { "", "abcd" })]
        [InlineData(new object[] { "abcd", null })]
        [InlineData(new object[] { "abcd", "" })]
        public void LoadPackageSourcesIgnoresInvalidCredentialPairsFromSettings(string userName, string password)
        {
            // Arrange
            var settings = new Mock<ISettings>();
            settings.Setup(s => s.GetSettingValues("packageSources", true))
                .Returns(new[]
                    {
                        new SettingValue("one", "onesource", false),
                        new SettingValue("two", "twosource", false),
                        new SettingValue("three", "threesource", false)
                    });

            settings.Setup(s => s.GetNestedValues("packageSourceCredentials", "two"))
                .Returns(new[] { new KeyValuePair<string, string>("Username", userName), new KeyValuePair<string, string>("Password", password) });

            var provider = CreatePackageSourceProvider(settings.Object);

            // Act
            var values = provider.LoadPackageSources().ToList();

            // Assert
            Assert.Equal(3, values.Count);
            AssertPackageSource(values[1], "two", "twosource", true);
            Assert.Null(values[1].UserName);
            Assert.Null(values[1].Password);
        }

        [Fact]
        public void LoadPackageSourcesReadsCredentialPairsFromSettings()
        {
            // Arrange
            string encryptedPassword = EncryptionUtility.EncryptString("topsecret");

            var settings = new Mock<ISettings>();
            settings.Setup(s => s.GetSettingValues("packageSources", true))
                .Returns(new[]
                    {
                        new SettingValue("one", "onesource", false),
                        new SettingValue("two", "twosource", false),
                        new SettingValue("three", "threesource", false)
                    });

            settings.Setup(s => s.GetNestedValues("packageSourceCredentials", "two"))
                .Returns(new[] { new KeyValuePair<string, string>("Username", "user1"), new KeyValuePair<string, string>("Password", encryptedPassword) });

            var provider = CreatePackageSourceProvider(settings.Object);

            // Act
            var values = provider.LoadPackageSources().ToList();

            // Assert
            Assert.Equal(3, values.Count);
            AssertPackageSource(values[1], "two", "twosource", true);
            Assert.Equal("user1", values[1].UserName);
            Assert.Equal("topsecret", values[1].Password);
            Assert.False(values[1].IsPasswordClearText);
        }

        [Fact]
        public void LoadPackageSourcesReadsClearTextCredentialPairsFromSettings()
        {
            // Arrange
            const string clearTextPassword = "topsecret";

            var settings = new Mock<ISettings>();
            settings.Setup(s => s.GetSettingValues("packageSources", true))
                .Returns(new[]
                    {
                        new SettingValue("one", "onesource", false),
                        new SettingValue("two", "twosource", false),
                        new SettingValue("three", "threesource", false)
                    });

            settings.Setup(s => s.GetNestedValues("packageSourceCredentials", "two"))
                .Returns(new[] { new KeyValuePair<string, string>("Username", "user1"), new KeyValuePair<string, string>("ClearTextPassword", clearTextPassword) });

            var provider = CreatePackageSourceProvider(settings.Object);

            // Act
            var values = provider.LoadPackageSources().ToList();

            // Assert
            Assert.Equal(3, values.Count);
            AssertPackageSource(values[1], "two", "twosource", true);
            Assert.Equal("user1", values[1].UserName);
            Assert.True(values[1].IsPasswordClearText);
            Assert.Equal("topsecret", values[1].Password);
        }

        [Fact]
        public void LoadPackageSourcesWhenEnvironmentCredentialsAreMalformedFallsbackToSettingsCredentials()
        {
            // Arrange
            var settings = new Mock<ISettings>();
            settings.Setup(s => s.GetSettingValues("packageSources", true))
                .Returns(new[]
                    {
                        new SettingValue("one", "onesource", false),
                        new SettingValue("two", "twosource", false),
                        new SettingValue("three", "threesource", false)
                    });
            settings.Setup(s => s.GetNestedValues("packageSourceCredentials", "two"))
                .Returns(new[] { new KeyValuePair<string, string>("Username", "settinguser"), new KeyValuePair<string, string>("ClearTextPassword", "settingpassword") });

            var provider = CreatePackageSourceProvider(settings.Object);

            // Act
            var values = provider.LoadPackageSources().ToList();

            // Assert
            Assert.Equal(3, values.Count);
            AssertPackageSource(values[1], "two", "twosource", true);
            Assert.Equal("settinguser", values[1].UserName);
            Assert.Equal("settingpassword", values[1].Password);
        }

        // Test that when there are duplicate sources, i.e. sources with the same name,
        // then the source specified in one Settings with the highest priority is used.
        [Fact]
        public void DuplicatePackageSources()
        {
            // Arrange
            var settings = new Mock<ISettings>();
            settings.Setup(s => s.GetSettingValues("packageSources", true))
                .Returns(new[]
                    {
                        new SettingValue("one", "onesource", false),
                        new SettingValue("two", "twosource", false, priority: 1),
                        new SettingValue("one", "threesource", false, priority: 1)
                    });

            var provider = CreatePackageSourceProvider(settings.Object);

            // Act
            var values = provider.LoadPackageSources().ToList();

            // Assert
            Assert.Equal(2, values.Count);
            AssertPackageSource(values[0], "two", "twosource", true);
            AssertPackageSource(values[1], "one", "threesource", true);
        }

        [Fact]
        public void SavePackageSourcesSaveCorrectDataToSettings()
        {
            // Arrange
            var sources = new[] { new PackageSource("one"), new PackageSource("two"), new PackageSource("three") };
            var settings = new Mock<ISettings>(MockBehavior.Strict);
            settings.Setup(s => s.DeleteSection("packageSourceCredentials")).Returns(true).Verifiable();

            settings.Setup(s => s.GetSettingValues("packageSources", true))
                .Returns(new SettingValue[0]);

            settings.Setup(s => s.GetSettingValues("disabledPackageSources", false))
                .Returns(new SettingValue[0]);

            settings.Setup(s => s.UpdateSections("packageSources", It.IsAny<IReadOnlyList<SettingValue>>()))
                .Callback((string section, IReadOnlyList<SettingValue> values) =>
                    {
                        Assert.Equal(3, values.Count);
                        Assert.Equal("one", values[0].Key);
                        Assert.Equal("one", values[0].Value);
                        Assert.Empty(values[0].AdditionalData);
                        Assert.Equal("two", values[1].Key);
                        Assert.Equal("two", values[1].Value);
                        Assert.Empty(values[1].AdditionalData);
                        Assert.Equal("three", values[2].Key);
                        Assert.Equal("three", values[2].Value);
                        Assert.Empty(values[2].AdditionalData);
                    })
                .Verifiable();

            settings.Setup(s => s.UpdateSections("disabledPackageSources", It.IsAny<IReadOnlyList<SettingValue>>()))
                .Callback((string section, IReadOnlyList<SettingValue> values) => { Assert.Empty(values); })
                .Verifiable();

            var provider = CreatePackageSourceProvider(settings.Object);

            // Act
            provider.SavePackageSources(sources);

            // Assert
            settings.Verify();
        }

        [Fact]
        public void SavePackage_KeepsBothNewAndOldSources()
        {
            // Arrange
            var sources = new[]
                {
                    new PackageSource("Source1", "Source1-Name"),
                    new PackageSource("Source2", "Source2-Name") { ProtocolVersion = 3 }
                };

            var settings = new Mock<ISettings>(MockBehavior.Strict);
            settings.Setup(s => s.DeleteSection("packageSourceCredentials")).Returns(true).Verifiable();

            settings.Setup(s => s.GetSettingValues("packageSources", true))
                .Returns(new[] { new SettingValue("Source2-Name", "Legacy-Source", isMachineWide: false) });

            settings.Setup(s => s.GetSettingValues("disabledPackageSources", false))
                .Returns(new SettingValue[0]);

            settings.Setup(s => s.UpdateSections("packageSources", It.IsAny<IReadOnlyList<SettingValue>>()))
                .Callback((string section, IReadOnlyList<SettingValue> values) =>
                    {
                        Assert.Collection(values,
                            value =>
                                {
                                    Assert.Equal("Source1-Name", value.Key);
                                    Assert.Equal("Source1", value.Value);
                                    Assert.Empty(value.AdditionalData);
                                },
                            value =>
                                {
                                    Assert.Equal("Source2-Name", value.Key);
                                    Assert.Equal("Legacy-Source", value.Value);
                                    Assert.Empty(value.AdditionalData);
                                },
                            value =>
                                {
                                    Assert.Equal("Source2-Name", value.Key);
                                    Assert.Equal("Source2", value.Value);
                                    Assert.Collection(value.AdditionalData,
                                        item =>
                                            {
                                                Assert.Equal("protocolVersion", item.Key);
                                                Assert.Equal("3", item.Value);
                                            });
                                });
                    })
                .Verifiable();

            settings.Setup(s => s.UpdateSections("disabledPackageSources", It.IsAny<IReadOnlyList<SettingValue>>()))
                .Callback((string section, IReadOnlyList<SettingValue> values) => { Assert.Empty(values); })
                .Verifiable();

            var provider = CreatePackageSourceProvider(settings.Object);

            // Act
            provider.SavePackageSources(sources);

            // Assert
            settings.Verify();
        }

        [Fact]
        public void SavePackageSourcesSaveCorrectDataToSettingsWhenSomePackageSourceIsDisabled()
        {
            // Arrange
            var sources = new[] { new PackageSource("one"), new PackageSource("two", "two", isEnabled: false), new PackageSource("three") };
            var settings = new Mock<ISettings>();
            settings.Setup(s => s.UpdateSections("disabledPackageSources", It.IsAny<IReadOnlyList<SettingValue>>()))
                .Callback((string section, IReadOnlyList<SettingValue> values) =>
                    {
                        var value = Assert.Single(values);
                        Assert.Equal("two", value.Key);
                        Assert.Equal("true", value.Value, StringComparer.OrdinalIgnoreCase);
                    })
                .Verifiable();

            var provider = CreatePackageSourceProvider(settings.Object);

            // Act
            provider.SavePackageSources(sources);

            // Assert
            settings.Verify();
        }

        [Fact]
        public void SavePackageSourcesSavesCredentials()
        {
            // Arrange
            var entropyBytes = Encoding.UTF8.GetBytes("NuGet");
            var sources = new[]
                {
                    new PackageSource("one"),
                    new PackageSource("twosource", "twoname") { UserName = "User", Password = "password" },
                    new PackageSource("three")
                };
            var settings = new Mock<ISettings>();
            settings.Setup(s => s.DeleteSection("packageSourceCredentials")).Returns(true).Verifiable();

            settings.Setup(s => s.SetNestedValues("packageSourceCredentials", It.IsAny<string>(), It.IsAny<IList<KeyValuePair<string, string>>>()))
                .Callback((string section, string key, IList<KeyValuePair<string, string>> values) =>
                    {
                        Assert.Equal("twoname", key);
                        Assert.Equal(2, values.Count);
                        AssertKVP(new KeyValuePair<string, string>("Username", "User"), values[0]);
                        Assert.Equal("Password", values[1].Key);
                        var decryptedPassword = Encoding.UTF8.GetString(
                            ProtectedData.Unprotect(Convert.FromBase64String(values[1].Value), entropyBytes, DataProtectionScope.CurrentUser));
                        Assert.Equal("Password", values[1].Key);
                        Assert.Equal("password", decryptedPassword);
                    })
                .Verifiable();

            var provider = CreatePackageSourceProvider(settings.Object);

            // Act
            provider.SavePackageSources(sources);

            // Assert
            settings.Verify();
        }

        [Fact]
        public void SavePackageSourcesSavesClearTextCredentials()
        {
            // Arrange
            var sources = new[]
                {
                    new PackageSource("one"),
                    new PackageSource("twosource", "twoname") { UserName = "User", Password = "password", IsPasswordClearText = true },
                    new PackageSource("three")
                };
            var settings = new Mock<ISettings>();
            settings.Setup(s => s.DeleteSection("packageSourceCredentials")).Returns(true).Verifiable();

            settings.Setup(s => s.SetNestedValues("packageSourceCredentials", It.IsAny<string>(), It.IsAny<IList<KeyValuePair<string, string>>>()))
                .Callback((string section, string key, IList<KeyValuePair<string, string>> values) =>
                    {
                        Assert.Equal("twoname", key);
                        Assert.Equal(2, values.Count);
                        AssertKVP(new KeyValuePair<string, string>("Username", "User"), values[0]);
                        AssertKVP(new KeyValuePair<string, string>("ClearTextPassword", "password"), values[1]);
                    })
                .Verifiable();

            var provider = CreatePackageSourceProvider(settings.Object);

            // Act
            provider.SavePackageSources(sources);

            // Assert
            settings.Verify();
        }

        [Fact]
        public void LoadPackageSourcesWithDisabledPackageSourceIsUpperCase()
        {
            // Arrange
            var settings = new Mock<ISettings>(MockBehavior.Strict);
            settings.Setup(s => s.GetSettingValues("packageSources", true))
                .Returns(new[]
                    {
                        new SettingValue("one", "onesource", false),
                        new SettingValue("TWO", "twosource", false),
                        new SettingValue("three", "threesource", false)
                    });
            settings.Setup(s => s.GetSettingValues("disabledPackageSources", false)).Returns(
                new[] { new SettingValue("TWO", "true", false) });
            settings.Setup(s => s.GetNestedValues("packageSourceCredentials", It.IsAny<string>())).Returns(new KeyValuePair<string, string>[0]);

            var provider = CreatePackageSourceProvider(settings.Object);

            // Act
            var values = provider.LoadPackageSources().ToList();

            // Assert
            Assert.Equal(3, values.Count);
            AssertPackageSource(values[0], "one", "onesource", true);
            AssertPackageSource(values[1], "TWO", "twosource", false);
            AssertPackageSource(values[2], "three", "threesource", true);
        }

        // Test that a source added in a high priority config file is not 
        // disabled by <disabledPackageSources> in a low priority file.
        [Fact]
        public void HighPrioritySourceNotDisabled()
        {
            // Arrange
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            var configContent1 = @"<configuration>
    <disabledPackageSources>
        <add key='a' value='true' />
    </disabledPackageSources>
</configuration>";
            var configContent2 = @"<configuration>
    <packageSources>
        <add key='a' value='http://a' />
    </packageSources>
</configuration>";
            TestFilesystemUtility.CreateConfigurationFile("nuget.config", Path.Combine(mockBaseDirectory, @"a\b"), configContent1);
            TestFilesystemUtility.CreateConfigurationFile("nuget.config", Path.Combine(mockBaseDirectory, @"a\b\c"), configContent2);

            var settings = Settings.LoadDefaultSettings(
                Path.Combine(mockBaseDirectory, @"a\b\c"),
                configFileName: null,
                machineWideSettings: null,
                loadAppDataSettings: false);

            var provider = CreatePackageSourceProvider(settings);
            // Act
            var values = provider.LoadPackageSources().ToList();

            // Assert
            Assert.Equal(1, values.Count);
            Assert.True(values[0].IsEnabled);
            Assert.Equal("a", values[0].Name);
            Assert.Equal("http://a", values[0].Source);
        }

        // Test that a source added in a low priority config file is disabled
        // if it's listed in <disabledPackageSources> in a high priority file.
        [Fact]
        public void LowPrioritySourceDisabled()
        {
            // Arrange
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            var configContent1 = @"<configuration>
    <disabledPackageSources>
        <add key='a' value='true' />
    </disabledPackageSources>
</configuration>";
            var configContent2 = @"<configuration>
    <packageSources>
        <add key='a' value='http://a' />
    </packageSources>
</configuration>";
            TestFilesystemUtility.CreateConfigurationFile("nuget.config", Path.Combine(mockBaseDirectory, @"a\b"), configContent2);
            TestFilesystemUtility.CreateConfigurationFile("nuget.config", Path.Combine(mockBaseDirectory, @"a\b\c"), configContent1);

            var settings = Settings.LoadDefaultSettings(
                Path.Combine(mockBaseDirectory, @"a\b\c"),
                configFileName: null,
                machineWideSettings: null,
                loadAppDataSettings: false);

            var provider = CreatePackageSourceProvider(settings);

            // Act
            var values = provider.LoadPackageSources().ToList();

            // Assert
            Assert.Equal(1, values.Count);
            Assert.False(values[0].IsEnabled);
            Assert.Equal("a", values[0].Name);
            Assert.Equal("http://a", values[0].Source);
        }

        [Fact]
        public void V2NotDisabled()
        {
            // Arrange
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            var configContent = @"<configuration>
    <packageSources>
        <add key='nuget.org' value='https://www.nuget.org/api/v2/' />
    </packageSources>
</configuration>";
            TestFilesystemUtility.CreateConfigurationFile("nuget.config", mockBaseDirectory, configContent);

            var settings = Settings.LoadDefaultSettings(
                mockBaseDirectory,
                configFileName: null,
                machineWideSettings: null,
                loadAppDataSettings: false);

            var provider = CreatePackageSourceProvider(settings);

            // Act
            var values = provider.LoadPackageSources().Where(p => p.Name.Equals("nuget.org", StringComparison.OrdinalIgnoreCase)).ToList();

            // Assert
            Assert.True(values[0].IsEnabled);
        }

        [Fact]
        public void AddPackageSourcesWithConfigFile()
        {
            
            using (var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var configContents =
                     @"<?xml version=""1.0""?>
<configuration>
<packageSources>
    <add key='NuGet.org' value='https://NuGet.org' />
</packageSources>
</configuration>
";
                File.WriteAllText(Path.Combine(mockBaseDirectory.Path, "NuGet.config"), configContents);

                var settings = Settings.LoadDefaultSettings(mockBaseDirectory.Path,
                   configFileName: "NuGet.config",
                   machineWideSettings: null,
                   loadAppDataSettings: true);
                var packageSourceProvider = new PackageSourceProvider(settings);

                // Act
                List<PackageSource> sources = packageSourceProvider.LoadPackageSources().ToList();
                sources.Add(new PackageSource("https://test.org", "test"));
                packageSourceProvider.SavePackageSources(sources);

                // Assert
                Assert.Equal(
                      @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
<packageSources>
 <add key=""NuGet.org"" value=""https://NuGet.org"" />
 <add key=""test"" value=""https://test.org"" />
</packageSources>
</configuration>
".Replace("\r\n", "\n"),
                  File.ReadAllText(Path.Combine(mockBaseDirectory.Path, "NuGet.config")).Replace("\r\n", "\n"));
            }
        }

        [Fact]
        public void SavePackageSources_AddDisabledSourceToTheConfigContainingSource()
        {
            using (var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var config1Contents =
                    @"<?xml version=""1.0""?>
<configuration>
<packageSources>
    <add key='NuGet.org' value='https://NuGet.org' />
</packageSources>
</configuration>
";
                File.WriteAllText(Path.Combine(mockBaseDirectory.Path, "NuGet.config"), config1Contents);

                var config2Contents =
                    @"<?xml version=""1.0"" encoding=""utf-8""?> <configuration></configuration>";
                var rootPath = Path.Combine(mockBaseDirectory.Path, Path.GetRandomFileName());
                Directory.CreateDirectory(rootPath);
                File.WriteAllText(Path.Combine(rootPath, "NuGet.config"), config2Contents);

                var settings = Settings.LoadDefaultSettings(rootPath,
                    configFileName: null,
                    machineWideSettings: null,
                    loadAppDataSettings: false);
                var packageSourceProvider = new PackageSourceProvider(settings);
                
                // Act - 1
                var sources = packageSourceProvider.LoadPackageSources();

                // Assert - 2
                var source = Assert.Single(sources);
                Assert.Equal("NuGet.org", source.Name);
                Assert.Equal("https://NuGet.org", source.Source);
                Assert.True(source.IsEnabled);

                // Act - 2
                source.IsEnabled = false;
                packageSourceProvider.SavePackageSources(sources);

                // Assert - 3
                Assert.Equal(config2Contents, File.ReadAllText(Path.Combine(rootPath, "NuGet.config")));
                Assert.Equal(
                        @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
<packageSources>
 <add key=""NuGet.org"" value=""https://NuGet.org"" />
</packageSources>
  <disabledPackageSources>
    <add key=""NuGet.org"" value=""true"" />
  </disabledPackageSources>
</configuration>
".Replace("\r\n", "\n"),
                    File.ReadAllText(Path.Combine(mockBaseDirectory.Path, "NuGet.config")).Replace("\r\n", "\n"));
            }
        }

        [Fact]
        public void SavePackageSources_WritesToTheSettingsFileWithTheNearestPriority()
        {
            using (var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var config1Contents =
                    @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>  <packageSources>    <add key=""NuGet.org"" value=""https://NuGet.org"" />  </packageSources>
</configuration>
".Replace("\r\n", "\n");
                var config2Contents =
                    @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
<packageSources>
    <add key='test.org' value='https://test.org' />
    <add key='NuGet.org' value='https://NuGet.org' />
</packageSources>
</configuration>
".Replace("\r\n", "\n");
                File.WriteAllText(Path.Combine(mockBaseDirectory.Path, "NuGet.config"), config1Contents);

                var rootPath = Path.Combine(mockBaseDirectory.Path, Path.GetRandomFileName());
                Directory.CreateDirectory(rootPath);
                File.WriteAllText(Path.Combine(rootPath, "NuGet.config"), config2Contents);

                var settings = Settings.LoadDefaultSettings(rootPath,
                    configFileName: null,
                    machineWideSettings: null,
                    loadAppDataSettings: false);
                var packageSourceProvider = new PackageSourceProvider(settings);

                // Act - 1
                var sources = packageSourceProvider.LoadPackageSources();

                // Assert - 1
                Assert.Collection(sources,
                    source =>
                        {
                            Assert.Equal("test.org", source.Name);
                            Assert.Equal("https://test.org", source.Source);
                            Assert.True(source.IsEnabled);
                        },
                    source =>
                        {
                            Assert.Equal("NuGet.org", source.Name);
                            Assert.Equal("https://NuGet.org", source.Source);
                            Assert.True(source.IsEnabled);
                        });

                // Act - 2
                sources.Last().IsEnabled = false;
                packageSourceProvider.SavePackageSources(sources);

                // Assert - 2
                Assert.Equal(config1Contents.Replace("\r\n", "\n"),
                    File.ReadAllText(Path.Combine(mockBaseDirectory.Path, "NuGet.config")).Replace("\r\n", "\n"));

                Assert.Equal(
                        @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
<packageSources>
 <add key=""test.org"" value=""https://test.org"" />
 <add key=""NuGet.org"" value=""https://NuGet.org"" />
</packageSources>
  <disabledPackageSources>
    <add key=""NuGet.org"" value=""true"" />
  </disabledPackageSources>
</configuration>
".Replace("\r\n", "\n"),
                    File.ReadAllText(Path.Combine(rootPath, "NuGet.config")).Replace("\r\n", "\n"));
            }
        }

        [Fact]
        public void SavePackageSources_UpdatesSourceInAllConfigs()
        {
            using (var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var config1Contents =
                    @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>  <packageSources>    <add key=""NuGet.org"" value=""https://NuGet.org"" />  </packageSources>
</configuration>
";
                var config2Contents =
                    @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
<packageSources>
    <add key='test.org' value='https://test.org' />
    <add key='NuGet.org' value='https://NuGet.org' />
</packageSources>
</configuration>
";
                File.WriteAllText(Path.Combine(mockBaseDirectory.Path, "NuGet.config"), config1Contents);

                var rootPath = Path.Combine(mockBaseDirectory.Path, Path.GetRandomFileName());
                Directory.CreateDirectory(rootPath);
                File.WriteAllText(Path.Combine(rootPath, "NuGet.config"), config2Contents);

                var settings = Settings.LoadDefaultSettings(rootPath,
                    configFileName: null,
                    machineWideSettings: null,
                    loadAppDataSettings: false);
                var packageSourceProvider = new PackageSourceProvider(settings);

                // Act - 1
                var sources = packageSourceProvider.LoadPackageSources().ToList();

                // Assert - 1
                Assert.Collection(sources,
                    source =>
                        {
                            Assert.Equal("test.org", source.Name);
                            Assert.Equal("https://test.org", source.Source);
                            Assert.True(source.IsEnabled);
                        },
                    source =>
                        {
                            Assert.Equal("NuGet.org", source.Name);
                            Assert.Equal("https://NuGet.org", source.Source);
                            Assert.True(source.IsEnabled);
                        });

                // Act - 2
                sources[1].Source = "https://new.NuGet.org";
                var sourcesToSave = new[] { sources[1], sources[0] };

                packageSourceProvider.SavePackageSources(sourcesToSave);

                // Assert - 2
                Assert.Equal(
                        @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>  <packageSources>    <add key=""NuGet.org"" value=""https://new.NuGet.org"" />  </packageSources>
</configuration>
".Replace("\r\n", "\n"),
                    File.ReadAllText(Path.Combine(mockBaseDirectory.Path, "NuGet.config")).Replace("\r\n", "\n"));

                Assert.Equal(
                         @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
<packageSources>
 <add key=""NuGet.org"" value=""https://new.NuGet.org"" />
 <add key=""test.org"" value=""https://test.org"" />
</packageSources>
</configuration>
".Replace("\r\n", "\n"),
                    File.ReadAllText(Path.Combine(rootPath, "NuGet.config")).Replace("\r\n", "\n"));
            }
        }

        [Fact]
        public void SavePackageSources_AddsNewSourcesToTheSettingWithLowestPriority()
        {
            using (var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var config1Contents =
                    @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>  <packageSources>    <add key=""test.org"" value=""https://test.org"" />  </packageSources>
</configuration>
".Replace("\r\n", "\n");
                var config2Contents =
                    @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
<packageSources>
    <add key='NuGet.org' value='https://NuGet.org' />
</packageSources>
</configuration>
".Replace("\r\n", "\n");
                File.WriteAllText(Path.Combine(mockBaseDirectory.Path, "NuGet.config"), config1Contents);

                var rootPath = Path.Combine(mockBaseDirectory.Path, Path.GetRandomFileName());
                Directory.CreateDirectory(rootPath);
                File.WriteAllText(Path.Combine(rootPath, "NuGet.config"), config2Contents);

                var settings = Settings.LoadDefaultSettings(rootPath,
                    configFileName: null,
                    machineWideSettings: null,
                    loadAppDataSettings: false);
                var packageSourceProvider = new PackageSourceProvider(settings);

                // Act - 1
                var sources = packageSourceProvider.LoadPackageSources().ToList();

                // Assert - 1
                Assert.Collection(sources,
                    source =>
                        {
                            Assert.Equal("NuGet.org", source.Name);
                            Assert.Equal("https://NuGet.org", source.Source);
                            Assert.True(source.IsEnabled);
                        },
                    source =>
                        {
                            Assert.Equal("test.org", source.Name);
                            Assert.Equal("https://test.org", source.Source);
                            Assert.True(source.IsEnabled);
                        });

                // Act - 2
                sources[1].IsEnabled = false;
                sources.Insert(1, new PackageSource("http://newsource", "NewSourceName"));

                packageSourceProvider.SavePackageSources(sources);

                // Assert - 2
                Assert.Equal(
@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>  <packageSources>    <add key=""NewSourceName"" value=""http://newsource"" />    <add key=""test.org"" value=""https://test.org"" />  </packageSources>
  <disabledPackageSources>
    <add key=""test.org"" value=""true"" />
  </disabledPackageSources>
</configuration>
".Replace("\r\n", "\n"),
                    File.ReadAllText(Path.Combine(mockBaseDirectory.Path, "NuGet.config")).Replace("\r\n", "\n"));

                Assert.Equal(
                        @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
<packageSources>
 <add key=""NuGet.org"" value=""https://NuGet.org"" />
</packageSources>
</configuration>
".Replace("\r\n", "\n"),
                    File.ReadAllText(Path.Combine(rootPath, "NuGet.config")).Replace("\r\n", "\n"));
            }
        }

        [Fact]
        public void SavePackageSources_AddsOrderingForCollapsedFeeds()
        {
            using (var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var configContents =
                    @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""nuget.org"" value=""https://nuget.org"" />
        <add key=""test.org"" value=""https://test.org"" />
        <add key=""test.org"" value=""https://new.test.org"" protocolVersion=""3"" />
        <add key=""test2"" value=""https://test2.net"" />
    </packageSources>
</configuration>
";
                File.WriteAllText(Path.Combine(mockBaseDirectory.Path, "NuGet.config"), configContents);

                var rootPath = Path.Combine(mockBaseDirectory.Path, Path.GetRandomFileName());

                var settings = Settings.LoadDefaultSettings(rootPath,
                    configFileName: null,
                    machineWideSettings: null,
                    loadAppDataSettings: false);
                var packageSourceProvider = new PackageSourceProvider(settings);

                // Act - 1
                var sources = packageSourceProvider.LoadPackageSources().ToList();

                // Assert - 2
                Assert.Collection(sources,
                    source =>
                        {
                            Assert.Equal("nuget.org", source.Name);
                            Assert.Equal("https://nuget.org", source.Source);
                            Assert.True(source.IsEnabled);
                        },
                    source =>
                        {
                            Assert.Equal("test.org", source.Name);
                            Assert.Equal("https://new.test.org", source.Source);
                            Assert.True(source.IsEnabled);
                            Assert.Equal(3, source.ProtocolVersion);
                        },
                    source =>
                        {
                            Assert.Equal("test2", source.Name);
                            Assert.Equal("https://test2.net", source.Source);
                            Assert.True(source.IsEnabled);
                        });

                // Act - 2
                sources[1].Source = "https://new2.test.org";
                var sourcesToSave = new[]
                    {
                        sources[1], sources[2], sources[0]
                    };
                packageSourceProvider.SavePackageSources(sourcesToSave);

                // Assert - 2
                Assert.Equal(
                        @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""test.org"" value=""https://test.org"" />
        <add key=""test.org"" value=""https://new2.test.org"" protocolVersion=""3"" />
        <add key=""test2"" value=""https://test2.net"" />
        <add key=""nuget.org"" value=""https://nuget.org"" />
    </packageSources>
</configuration>
".Replace("\r\n", "\n"),
                    File.ReadAllText(Path.Combine(mockBaseDirectory.Path, "NuGet.config")).Replace("\r\n", "\n"));
            }
        }

        [Fact]
        public void SavePackageSources_DisabledMachineWideSource()
        {
            using (var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var configContents =
                    @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""Microsoft and .NET""
         value = ""https://www.nuget.org/api/v2/curated-feeds/microsoftdotnet/"" />
    </packageSources>
</configuration>
";
                var configContets1 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
</configuration>
";
                File.WriteAllText(Path.Combine(mockBaseDirectory.Path, "machinewide.config"), configContents);
                File.WriteAllText(Path.Combine(mockBaseDirectory.Path, "NuGet.Config"), configContets1);
                var machineWideSetting = new Settings(mockBaseDirectory.Path, "machinewide.config", true);
                var m = new Mock<IMachineWideSettings>();
                m.SetupGet(obj => obj.Settings).Returns(new List<Settings> { machineWideSetting });

                var settings = Settings.LoadDefaultSettings(mockBaseDirectory.Path,
                   configFileName: null,
                   machineWideSettings: m.Object,
                   loadAppDataSettings: false);
                var packageSourceProvider = new PackageSourceProvider(settings);
                var sources = packageSourceProvider.LoadPackageSources().ToList();

                // Act
                foreach (var source in sources)
                {
                    source.IsEnabled = false;
                }
                packageSourceProvider.SavePackageSources(sources);

                // Assert
                var newSources = packageSourceProvider.LoadPackageSources().ToList();
                foreach (var source in sources)
                {
                    Assert.False(source.IsEnabled);
                }
            }
        }

        [Fact]
        public void SavePackageSources_DisabledOneMachineWideSource()
        {
            using (var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var configContents =
                    @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""Microsoft and .NET""
         value = ""https://www.nuget.org/api/v2/curated-feeds/microsoftdotnet/"" />
        <add key=""test1""
         value = ""//test/source"" />
    </packageSources>
</configuration>
";
                var configContents1 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
</configuration>
";
                File.WriteAllText(Path.Combine(mockBaseDirectory.Path, "machinewide.config"), configContents);
                File.WriteAllText(Path.Combine(mockBaseDirectory.Path, "NuGet.Config"), configContents1);
                var machineWideSetting = new Settings(mockBaseDirectory.Path, "machinewide.config", true);
                var m = new Mock<IMachineWideSettings>();
                m.SetupGet(obj => obj.Settings).Returns(new List<Settings> { machineWideSetting });

                var settings = Settings.LoadDefaultSettings(mockBaseDirectory.Path,
                   configFileName: null,
                   machineWideSettings: m.Object,
                   loadAppDataSettings: false);
                var packageSourceProvider = new PackageSourceProvider(settings);
                var sources = packageSourceProvider.LoadPackageSources().ToList();

                // Act
                sources[1].IsEnabled = false;
                packageSourceProvider.SavePackageSources(sources);

                // Assert
                var newSources = packageSourceProvider.LoadPackageSources().ToList();
                Assert.True(newSources[0].IsEnabled);
                Assert.Equal("Microsoft and .NET", newSources[0].Name);

                Assert.False(newSources[1].IsEnabled);
                Assert.Equal("test1", newSources[1].Name);
            }
        }

        [Fact(Skip = "Test currently failing")]
        public void DisabledMachineWideSourceByDefault()
        {
            using (var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var configContents =
                    @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""Microsoft and .NET""
         value = ""https://www.nuget.org/api/v2/curated-feeds/microsoftdotnet/"" />
        <add key=""test1""
         value = ""//test/source"" />
    </packageSources>
</configuration>
";
                File.WriteAllText(Path.Combine(mockBaseDirectory.Path, "machinewide.config"), configContents);
                var machineWideSetting = new Settings(mockBaseDirectory.Path, "machinewide.config", true);
                var m = new Mock<IMachineWideSettings>();
                m.SetupGet(obj => obj.Settings).Returns(new List<Settings> { machineWideSetting });

                // Act
                var settings = Settings.LoadDefaultSettings(mockBaseDirectory.Path,
                      configFileName: null,
                      machineWideSettings: m.Object,
                      loadAppDataSettings: true);
                var packageSourceProvider = new PackageSourceProvider(settings);
                var sources = packageSourceProvider.LoadPackageSources().ToList();

                // Assert
                Assert.False(sources[0].IsEnabled);
                Assert.False(sources[1].IsEnabled);
            }
        }

        [Fact(Skip = "Test currently failing")]
        public void DisabledMachineWideSourceByDefaultWithNull()
        {
            using (var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var settings = Settings.LoadDefaultSettings(mockBaseDirectory.Path,
                                  configFileName: null,
                                  machineWideSettings: null,
                                  loadAppDataSettings: true);
                var packageSourceProvider = new PackageSourceProvider(settings);

                // Act
                var sources = packageSourceProvider.LoadPackageSources().ToList();

                // Assert
                Assert.Equal(2, sources.Count);
            }
        }
        
        private string CreateNuGetConfigContent(string enabledReplacement = "", string disabledReplacement = "", string activeSourceReplacement = "")
        {
            var nugetConfigBaseString = new StringBuilder();
            nugetConfigBaseString.AppendLine(@"<?xml version='1.0' encoding='utf-8'?>");
            nugetConfigBaseString.AppendLine("<configuration>");
            nugetConfigBaseString.AppendLine("<packageRestore>");
            nugetConfigBaseString.AppendLine(@"<add key='enabled' value='True' />");
            nugetConfigBaseString.AppendLine(@"<add key='automatic' value='True' />");
            nugetConfigBaseString.AppendLine("</packageRestore>");
            nugetConfigBaseString.AppendLine("<packageSources>");
            nugetConfigBaseString.AppendLine("[EnabledSources]");
            nugetConfigBaseString.AppendLine("</packageSources>");
            nugetConfigBaseString.AppendLine("<disabledPackageSources>");
            nugetConfigBaseString.AppendLine("[DisabledSources]");
            nugetConfigBaseString.AppendLine("</disabledPackageSources>");
            nugetConfigBaseString.AppendLine("<activePackageSource>");
            nugetConfigBaseString.AppendLine("[ActiveSource]");
            nugetConfigBaseString.AppendLine("</activePackageSource>");
            nugetConfigBaseString.AppendLine("</configuration>");

            var nugetConfig = nugetConfigBaseString.ToString();
            nugetConfig = nugetConfig.Replace("[EnabledSources]", enabledReplacement);
            nugetConfig = nugetConfig.Replace("[DisabledSources]", disabledReplacement);
            nugetConfig = nugetConfig.Replace("[ActiveSource]", activeSourceReplacement);
            return nugetConfig;
        }

        private void VerifyPackageSource(PackageSourceProvider psp, int count, string[] names, string[] feeds, bool[] isEnabled, bool[] isOfficial)
        {
            List<PackageSource> toVerifyList = new List<PackageSource>();
            toVerifyList = psp.LoadPackageSources().ToList();

            Assert.Equal(toVerifyList.Count, count);
            var index = 0;
            foreach (PackageSource ps in toVerifyList)
            {
                Assert.Equal(ps.Name, names[index]);
                Assert.Equal(ps.Source, feeds[index]);
                Assert.Equal(ps.IsEnabled, isEnabled[index]);
                Assert.Equal(ps.IsOfficial, isOfficial[index]);
                index++;
            }
        }

        private IPackageSourceProvider CreatePackageSourceProvider(
            ISettings settings = null,
            IEnumerable<PackageSource> providerDefaultPrimarySources = null,
            IEnumerable<PackageSource> providerDefaultSecondarySources = null,
            IDictionary<PackageSource, PackageSource> migratePackageSources = null
            )
        {
            settings = settings ?? new Mock<ISettings>().Object;
            return new PackageSourceProvider(settings, providerDefaultPrimarySources, providerDefaultSecondarySources, migratePackageSources);
        }

        private void AssertPackageSource(PackageSource ps, string name, string source, bool isEnabled, bool isMachineWide = false, bool isOfficial = false)
        {
            Assert.Equal(name, ps.Name);
            Assert.Equal(source, ps.Source);
            Assert.True(ps.IsEnabled == isEnabled);
            Assert.True(ps.IsMachineWide == isMachineWide);
            Assert.True(ps.IsOfficial == isOfficial);
        }

        private static void AssertKVP(KeyValuePair<string, string> expected, KeyValuePair<string, string> actual)
        {
            Assert.Equal(expected.Key, actual.Key);
            Assert.Equal(expected.Value, actual.Value);
        }
    }
}
