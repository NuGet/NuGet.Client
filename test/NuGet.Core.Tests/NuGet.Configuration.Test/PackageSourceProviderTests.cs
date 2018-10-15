// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FluentAssertions;
using Moq;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Configuration.Test
{
    public class PackageSourceProviderTests
    {
        [Fact]
        public void ActivePackageSourceCanBeReadAndWrittenInNuGetConfig()
        {
            // Act
            //Create nuget.config that has active package source defined
            using (var nugetConfigFileFolder = TestDirectory.CreateInTemp())
            {
                var nugetConfigFilePath = Path.Combine(nugetConfigFileFolder, "nuget.Config");

                var enabledReplacement = @"<add key='" + NuGetConstants.FeedName + "' value='" + NuGetConstants.V2FeedUrl + "' />";
                var disabledReplacement = string.Empty;
                var activeReplacement = @"<add key='" + NuGetConstants.FeedName + "' value='" + NuGetConstants.V2FeedUrl + "' />";
                File.WriteAllText(nugetConfigFilePath, CreateNuGetConfigContent(enabledReplacement, disabledReplacement, activeReplacement));

                var settings = new Settings(nugetConfigFileFolder, "nuget.Config");

                var before = new PackageSourceProvider(settings);
                Assert.Equal(NuGetConstants.FeedName, before.ActivePackageSourceName);

                before.SaveActivePackageSource(new PackageSource(NuGetConstants.V3FeedUrl, NuGetConstants.FeedName));
                Assert.Equal(NuGetConstants.FeedName, before.ActivePackageSourceName);
            }
        }

        [Fact]
        public void ActivePackageSourceReturnsNullIfNotSetInNuGetConfig()
        {
            // Act
            //Create nuget.config that has active package source defined
            using (var nugetConfigFileFolder = TestDirectory.CreateInTemp())
            {
                var nugetConfigFilePath = Path.Combine(nugetConfigFileFolder, "nuget.Config");

                var enabledReplacement = @"<add key='" + NuGetConstants.FeedName + "' value='" + NuGetConstants.V2FeedUrl + "' />";
                var text = CreateNuGetConfigContent(enabledReplacement);
                File.WriteAllText(nugetConfigFilePath, text);

                var settings = new Settings(nugetConfigFileFolder, "nuget.config");
                var before = new PackageSourceProvider(settings);
                Assert.Null(before.ActivePackageSourceName);

                before.SaveActivePackageSource(new PackageSource(NuGetConstants.V3FeedUrl, NuGetConstants.FeedName));
                Assert.Equal(NuGetConstants.FeedName, before.ActivePackageSourceName);
            }
        }

        [Fact]
        public void ActivePackageSourceReturnsNullIfNotPresentInNuGetConfig()
        {
            // Act
            //Create nuget.config that has active package source defined
            using (var nugetConfigFileFolder = TestDirectory.CreateInTemp())
            {
                var nugetConfigFilePath = Path.Combine(nugetConfigFileFolder, "nuget.Config");

                var enabledReplacement = @"<add key='" + NuGetConstants.FeedName + "' value='" + NuGetConstants.V2FeedUrl + "' />";
                var fileContents = CreateNuGetConfigContent(enabledReplacement);
                fileContents = fileContents.Replace("<activePackageSource>", string.Empty);
                fileContents = fileContents.Replace("</activePackageSource>", string.Empty);
                File.WriteAllText(nugetConfigFilePath, fileContents);

                var settings = new Settings(nugetConfigFileFolder, "nuget.Config");
                var before = new PackageSourceProvider(settings);
                Assert.Null(before.ActivePackageSourceName);

                before.SaveActivePackageSource(new PackageSource(NuGetConstants.V3FeedUrl, NuGetConstants.FeedName));
                Assert.Equal(NuGetConstants.FeedName, before.ActivePackageSourceName);
            }
        }

        [Fact]
        public void LoadPackageSources_LoadsCredentials()
        {
            // Arrange
            var nugetConfigFilePath = "NuGet.Config";
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
      <add key='ClearTextPassword' value='pass' />
    </CodeCrackerUnstable>
    <AspNetVNextUnstable>
      <add key='Username' value='myusername' />
      <add key='ClearTextPassword' value='pass' />
    </AspNetVNextUnstable>
    <AspNetVNextStable>
      <add key='Username' value='myusername' />
      <add key='ClearTextPassword' value='pass' />
    </AspNetVNextStable>
    <NuGet.org>
      <add key='Username' value='myusername' />
      <add key='ClearTextPassword' value='pass' />
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

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                SettingsTestUtils.CreateConfigurationFile(nugetConfigFilePath, mockBaseDirectory, configContent);
                var settings = new Settings(mockBaseDirectory);
                var packageSourceProvider = new PackageSourceProvider(settings);

                // Act
                var sources = packageSourceProvider.LoadPackageSources().ToList();

                // Assert
                sources.Count.Should().Be(6);
                AssertCredentials(sources[1].Credentials, "CompanyFeedUnstable", "myusername", "removed");
                AssertCredentials(sources[5].Credentials, "CompanyFeed", "myusername", "removed");
            }
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
        public void LoadPackageSourcesReturnsEmptySequence()
        {
            // Arrange
            var settings = new Mock<ISettings>();
            var provider = CreatePackageSourceProvider(settings.Object);

            // Act
            var values = provider.LoadPackageSources();

            // Assert
            Assert.False(values.Any());
        }

        [Fact]
        public void SavePackageSourcesTest()
        {
            // Arrange
            var nugetConfigFilePath = "NuGet.Config";
            var configContent = @"<?xml version='1.0' encoding='utf-8'?>
<configuration>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                SettingsTestUtils.CreateConfigurationFile(nugetConfigFilePath, mockBaseDirectory, configContent);
                var settings = new Settings(mockBaseDirectory);
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

                var configFileContent = File.ReadAllText(Path.Combine(mockBaseDirectory, nugetConfigFilePath));
                var result = SettingsTestUtils.RemoveWhitespace(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""a"" value=""http://a"" />
    <add key=""b"" value=""http://b"" />
  </packageSources>
  <disabledPackageSources>
    <add key=""b"" value=""true"" />
    <add key=""d"" value=""true"" />
  </disabledPackageSources>
</configuration>");

                Assert.Equal(result, SettingsTestUtils.RemoveWhitespace(configFileContent));
            }
        }

        [Fact]
        public void SavePackageSourcesWithRelativePath()
        {
            using (var mockBaseDirectory = TestDirectory.CreateInTemp())
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

                var settings = Settings.LoadSettings(rootPath,
                    configFileName: null,
                    machineWideSettings: null,
                    loadUserWideSettings: false,
                    useTestingGlobalPath: false);
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
            using (var mockBaseDirectory = TestDirectory.CreateInTemp())
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

                var settings = Settings.LoadSettings(rootPath,
                    configFileName: null,
                    machineWideSettings: null,
                    loadUserWideSettings: false,
                    useTestingGlobalPath: false);
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
            using (var mockBaseDirectory = TestDirectory.CreateInTemp())
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

                var settings = Settings.LoadSettings(rootPath,
                    configFileName: null,
                    machineWideSettings: null,
                    loadUserWideSettings: false,
                    useTestingGlobalPath: false);
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
        public void SavePackageSourcesWithMoreClear()
        {
            using (var mockBaseDirectory = TestDirectory.CreateInTemp())
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

                var settings = Settings.LoadSettings(rootPath,
                    configFileName: null,
                    machineWideSettings: null,
                    loadUserWideSettings: false,
                    useTestingGlobalPath: false);
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
            using (var mockBaseDirectory = TestDirectory.CreateInTemp())
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

                var settings = Settings.LoadSettings(rootPath,
                    configFileName: null,
                    machineWideSettings: null,
                    loadUserWideSettings: false,
                    useTestingGlobalPath: false);
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
            using (var mockBaseDirectory = TestDirectory.CreateInTemp())
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
                SettingsTestUtils.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, "dir1", "dir2"), config);
                config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""key1"" value=""https://test.org/1"" />
    <clear />
  </packageSources>
</configuration>";
                SettingsTestUtils.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, "dir1"), config);

                var rootPath = Path.Combine(Path.Combine(mockBaseDirectory, "dir1", "dir2"), Path.GetRandomFileName());
                var settings = Settings.LoadSettings(rootPath,
                    configFileName: null,
                    machineWideSettings: null,
                    loadUserWideSettings: false,
                    useTestingGlobalPath: false);

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
                   File.ReadAllText(Path.Combine(mockBaseDirectory.Path, "dir1", "dir2", "NuGet.Config")).Replace("\r\n", "\n"));

                Assert.Equal(
                     @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""key1"" value=""https://test.org/1"" />
    <clear />
  </packageSources>
</configuration>".Replace("\r\n", "\n"),
                  File.ReadAllText(Path.Combine(mockBaseDirectory.Path, "dir1", "NuGet.Config")).Replace("\r\n", "\n"));
            }
        }

        [Fact]
        public void SavePackageSources_RetainUnavailableDisabledSources()
        {
            using (var mockBaseDirectory = TestDirectory.CreateInTemp())
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

                var settings = Settings.LoadSettings(rootPath,
                    configFileName: null,
                    machineWideSettings: null,
                    loadUserWideSettings: false,
                    useTestingGlobalPath: false);

                var disabledPackagesSection = settings.GetSection("disabledPackageSources");
                disabledPackagesSection.Should().NotBeNull();

                var expectedDisabledSources = disabledPackagesSection?.Items.ToList();

                var packageSourceProvider = new PackageSourceProvider(settings);
                var packageSourceList = packageSourceProvider.LoadPackageSources().ToList();

                // Act
                packageSourceProvider.SavePackageSources(packageSourceList);

                var newSettings = Settings.LoadSettings(rootPath,
                    configFileName: null,
                    machineWideSettings: null,
                    loadUserWideSettings: false,
                    useTestingGlobalPath: false);

                var actualDisabledSourcesSection = newSettings.GetSection("disabledPackageSources");
                actualDisabledSourcesSection.Should().NotBeNull();

                var actualDisabledSources = actualDisabledSourcesSection?.Items.ToList();

                Assert.Equal(expectedDisabledSources.Count, actualDisabledSources.Count);
                foreach (var item in expectedDisabledSources)
                {
                    Assert.Contains(item, actualDisabledSources);
                }
            }
        }

        [Fact]
        public void SavePackageSources_EnablesDisabledSources()
        {
            using (var mockBaseDirectory = TestDirectory.CreateInTemp())
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

                var settings = Settings.LoadSettings(rootPath,
                    configFileName: null,
                    machineWideSettings: null,
                    loadUserWideSettings: false,
                    useTestingGlobalPath: false);

                var disabledPackagesSection = settings.GetSection("disabledPackageSources");
                disabledPackagesSection.Should().NotBeNull();

                var disabledSources = disabledPackagesSection?.Items.Select(c => c as AddItem).ToList();

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

                var newSettings = Settings.LoadSettings(rootPath,
                    configFileName: null,
                    machineWideSettings: null,
                    loadUserWideSettings: false,
                    useTestingGlobalPath: false);

                // Main Assert
                disabledPackagesSection = newSettings.GetSection("disabledPackageSources");
                disabledPackagesSection.Should().NotBeNull();

                disabledSources = disabledPackagesSection?.Items.Select(c => c as AddItem).ToList();

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
        public void LoadPackageSourcesReturnCorrectDataFromSettings()
        {
            // Arrange
            var settings = new Mock<ISettings>(MockBehavior.Strict);

            settings.Setup(s => s.GetSection("packageSources"))
                .Returns(new VirtualSettingSection("packageSources",
                    new SourceItem("one", "onesource"),
                    new SourceItem("two", "twosource"),
                    new SourceItem("three", "threesource")
                ))
                .Verifiable();

            settings.Setup(s => s.GetSection("disabledPackageSources"))
                .Returns(new VirtualSettingSection("disabledPackageSources"));
            settings.Setup(s => s.GetSection("packageSourceCredentials"))
                .Returns(new VirtualSettingSection("packageSourceCredentials"));
            settings.Setup(s => s.GetSection("config"))
                .Returns(new VirtualSettingSection("config"));

            var provider = CreatePackageSourceProvider(settings.Object);

            // Act
            var values = provider.LoadPackageSources().ToList();

            // Assert
            Assert.Equal(3, values.Count);
            AssertPackageSource(values[0], "one", "onesource", isEnabled: true);
            AssertPackageSource(values[1], "two", "twosource", isEnabled: true);
            AssertPackageSource(values[2], "three", "threesource", isEnabled: true);
        }

        [Fact]
        public void LoadPackageSourcesReturnCorrectDataFromSettingsWhenSomePackageSourceIsDisabled()
        {
            // Arrange
            var settings = new Mock<ISettings>(MockBehavior.Strict);
            settings.Setup(s => s.GetSection("packageSources"))
                .Returns(new VirtualSettingSection("packageSources",
                        new SourceItem("one", "onesource"),
                        new SourceItem("two", "twosource"),
                        new SourceItem("three", "threesource")
                    ));

            settings.Setup(s => s.GetSection("disabledPackageSources"))
                .Returns(new VirtualSettingSection("packageSources",
                    new AddItem("two", "true")
                    ));

            settings.Setup(s => s.GetSection("packageSourceCredentials"))
                .Returns(new VirtualSettingSection("packageSourceCredentials"));

            settings.Setup(s => s.GetSection("config"))
                .Returns(new VirtualSettingSection("config"));

            var provider = CreatePackageSourceProvider(settings.Object);

            // Act
            var values = provider.LoadPackageSources().ToList();

            // Assert
            Assert.Equal(3, values.Count);
            AssertPackageSource(values[0], "one", "onesource", isEnabled: true);
            AssertPackageSource(values[1], "two", "twosource", isEnabled: false);
            AssertPackageSource(values[2], "three", "threesource", isEnabled: true);
        }

        [Fact]
        public void LoadPackageSources_ReadsSourcesWithProtocolVersionFromPackageSourceSections()
        {
            // Arrange
            var settings = new Mock<ISettings>();
            var settingWithV3Protocol1 = new SourceItem("Source2", "https://source-with-newer-protocol", "3");
            var settingWithV3Protocol2 = new SourceItem("Source3", "Source3", "3");

            settings.Setup(s => s.GetSection("packageSources"))
                .Returns(new VirtualSettingSection("packageSources",
                    new SourceItem("Source1", "https://some-source.org"),
                    settingWithV3Protocol1,
                    settingWithV3Protocol2,
                    new SourceItem("Source3", "Source3"),
                    new SourceItem("Source4", "//Source4")));

            settings
                .Setup(s => s.GetSection("packageSourceCredentials"))
                .Returns(new VirtualSettingSection("packageSourceCredentials",
                    new CredentialsItem("Source3", "source3-user", "source3-password", isPasswordClearText: true)));

            settings
                .Setup(s => s.GetSection("disabledPackageSources"))
                .Returns(new VirtualSettingSection("disabledPackageSources",
                        new AddItem("Source4", "true")
                    ));

            var provider = CreatePackageSourceProvider(
                settings.Object);

            // Act
            var values = provider.LoadPackageSources().ToList();

            // Assert
            Assert.Collection(values,
                source =>
                    {
                        Assert.Equal("Source1", source.Name);
                        Assert.Equal("https://some-source.org", source.Source);
                        Assert.Null(source.Credentials);
                        Assert.Equal(2, source.ProtocolVersion);
                        Assert.True(source.IsEnabled);
                    },
                source =>
                    {
                        Assert.Equal("Source2", source.Name);
                        Assert.Equal("https://source-with-newer-protocol", source.Source);
                        Assert.Null(source.Credentials);
                        Assert.Equal(3, source.ProtocolVersion);
                        Assert.True(source.IsEnabled);
                    },
                source =>
                    {
                        Assert.Equal("Source3", source.Name);
                        Assert.Equal("Source3", source.Source);
                        AssertCredentials(source.Credentials, "Source3", "source3-user", "source3-password");
                        Assert.Equal(3, source.ProtocolVersion);
                        Assert.True(source.IsEnabled);
                    },
                source =>
                    {
                        Assert.Equal("Source4", source.Name);
                        Assert.Equal("//Source4", source.Source);
                        Assert.Null(source.Credentials);
                        Assert.Equal(2, source.ProtocolVersion);
                        Assert.False(source.IsEnabled);
                    });
        }

        [Fact]
        public void DisablePackageSourceAddEntryToSettings()
        {
            // Arrange
            var settings = new Mock<ISettings>(MockBehavior.Strict);
            settings.Setup(s => s.GetSection("packageSources"))
                .Returns(() => null).Verifiable();
            settings.Setup(s => s.AddOrUpdate("disabledPackageSources", It.IsAny<AddItem>())).Verifiable();
            settings.Setup(s => s.SaveToDisk()).Verifiable();


            var provider = CreatePackageSourceProvider(settings.Object);

            // Act
            provider.DisablePackageSource("A");

            // Assert
            settings.Verify();
        }

        [Fact]
        public void IsPackageSourceEnabledReturnsFalseIfTheSourceIsDisabled()
        {
            // Arrange
            var settings = new Mock<ISettings>(MockBehavior.Strict);
            settings.Setup(s => s.GetSection("disabledPackageSources"))
                .Returns(new VirtualSettingSection("disabledPackageSources",
                    new AddItem("A", "sdfds")
                    ));
            var provider = CreatePackageSourceProvider(settings.Object);

            // Act
            var isEnabled = provider.IsPackageSourceEnabled("A");

            // Assert
            Assert.False(isEnabled);
        }

        [Fact]
        public void LoadPackageSources_ReadsCredentialPairsFromSettings()
        {
            // Arrange
            var encryptedPassword = Guid.NewGuid().ToString();

            var settings = new Mock<ISettings>();
            settings
                .Setup(s => s.GetSection("packageSources"))
                .Returns(new VirtualSettingSection("packageSources",
                    new SourceItem("one", "onesource"),
                    new SourceItem("two", "twosource"),
                    new SourceItem("three", "threesource")
                ));

            settings
                .Setup(s => s.GetSection("packageSourceCredentials"))
                .Returns(new VirtualSettingSection("two",
                    new CredentialsItem("two", "user1", encryptedPassword, isPasswordClearText: false)
                    ));

            var provider = CreatePackageSourceProvider(settings.Object);

            // Act
            var values = provider.LoadPackageSources().ToList();

            // Assert
            Assert.Equal(3, values.Count);
            AssertPackageSource(values[1], "two", "twosource", true);
            AssertCredentials(values[1].Credentials, "two", "user1", encryptedPassword, isPasswordClearText: false);
        }

        [Fact]
        public void LoadPackageSources_ReadsClearTextCredentialPairsFromSettings()
        {
            // Arrange
            const string clearTextPassword = "topsecret";

            var settings = new Mock<ISettings>();
            settings
                .Setup(s => s.GetSection("packageSources"))
                .Returns(new VirtualSettingSection("packageSources",
                    new SourceItem("one", "onesource"),
                    new SourceItem("two", "twosource"),
                    new SourceItem("three", "threesource")
                ));

            settings
             .Setup(s => s.GetSection("packageSourceCredentials"))
             .Returns(new VirtualSettingSection("two",
                 new CredentialsItem("two", "user1", clearTextPassword, isPasswordClearText: true)
                 ));

            var provider = CreatePackageSourceProvider(settings.Object);

            // Act
            var values = provider.LoadPackageSources().ToList();

            // Assert
            Assert.Equal(3, values.Count);
            AssertPackageSource(values[1], "two", "twosource", true);
            AssertCredentials(values[1].Credentials, "two", "user1", clearTextPassword);
        }

        [Fact]
        public void LoadPackageSources_WhenEnvironmentCredentialsAreMalformed_FallsbackToSettingsCredentials()
        {
            // Arrange
            var settings = new Mock<ISettings>();
            settings
                .Setup(s => s.GetSection("packageSources"))
                .Returns(new VirtualSettingSection("packageSources",
                    new SourceItem("one", "onesource"),
                    new SourceItem("two", "twosource"),
                    new SourceItem("three", "threesource")
                ));

            settings
                .Setup(s => s.GetSection("packageSourceCredentials"))
                .Returns(new VirtualSettingSection("two",
                    new CredentialsItem("two", "settinguser", "settingpassword", isPasswordClearText: true)
                    ));

            var provider = CreatePackageSourceProvider(settings.Object);

            // Act
            var values = provider.LoadPackageSources().ToList();

            // Assert
            Assert.Equal(3, values.Count);
            AssertPackageSource(values[1], "two", "twosource", true);
            AssertCredentials(values[1].Credentials, "two", "settinguser", "settingpassword");
        }

        [Fact]
        public void SavePackageSourcesSaveCorrectDataToSettings()
        {
            using (var mockBaseDirectory = TestDirectory.CreateInTemp())
            {
                // Arrange
                var configContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
</configuration>";

                File.WriteAllText(Path.Combine(mockBaseDirectory.Path, "NuGet.Config"), configContents);
                var settings = new Settings(mockBaseDirectory);
                var packageSourceProvider = new PackageSourceProvider(settings);
                var sources = new[] { new PackageSource("one"), new PackageSource("two"), new PackageSource("three") };

                // Act
                packageSourceProvider.SavePackageSources(sources);

                // Assert
                var packageSourcesSection = settings.GetSection("packageSources");
                packageSourcesSection.Should().NotBeNull();
                packageSourcesSection.Items.Count.Should().Be(3);
                packageSourcesSection.Items.Should().AllBeOfType<SourceItem>();

                var children = packageSourcesSection.Items.Select(c => c as SourceItem).ToList();
                children[0].Key.Should().Be("one");
                children[0].ProtocolVersion.Should().BeNullOrEmpty();
                children[1].Key.Should().Be("two");
                children[1].ProtocolVersion.Should().BeNullOrEmpty();
                children[2].Key.Should().Be("three");
                children[2].ProtocolVersion.Should().BeNullOrEmpty();

                var disabledPackageSourcesSection = settings.GetSection("disabledPackageSources");
                disabledPackageSourcesSection.Should().BeNull();
            }
        }

        [Fact]
        public void SavePackage_KeepsBothNewAndOldSources()
        {
            using (var mockBaseDirectory = TestDirectory.CreateInTemp())
            {
                // Arrange
                var configContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""Source2-Name"" value=""Legacy-Source"" />
    </packageSources>
</configuration>";

                File.WriteAllText(Path.Combine(mockBaseDirectory.Path, "NuGet.Config"), configContents);
                var settings = new Settings(mockBaseDirectory);
                var packageSourceProvider = new PackageSourceProvider(settings);
                var sources = new[]
                    {
                    new PackageSource("Source1", "Source1-Name"),
                    new PackageSource("Source2", "Source2-Name") { ProtocolVersion = 3 }
               };
                // Act
                packageSourceProvider.SavePackageSources(sources);

                // Assert
                var packageSourcesSection = settings.GetSection("packageSources");
                packageSourcesSection.Should().NotBeNull();
                packageSourcesSection.Items.Count.Should().Be(3);
                packageSourcesSection.Items.Should().AllBeOfType<SourceItem>();

                var children = packageSourcesSection.Items.Select(c => c as SourceItem).ToList();
                children[0].Key.Should().Be("Source2-Name");
                children[0].ProtocolVersion.Should().BeNullOrEmpty();
                children[1].Key.Should().Be("Source1-Name");
                children[1].ProtocolVersion.Should().BeNullOrEmpty();
                children[2].Key.Should().Be("Source2-Name");
                children[2].ProtocolVersion.Should().Be("3");

                var disabledPackageSourcesSection = settings.GetSection("disabledPackageSources");
                disabledPackageSourcesSection.Should().BeNull();
            }
        }

        [Fact]
        public void SavePackageSourcesSaveCorrectDataToSettingsWhenSomePackageSourceIsDisabled()
        {

            using (var mockBaseDirectory = TestDirectory.CreateInTemp())
            {
                // Arrange
                var configContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
</configuration>";

                File.WriteAllText(Path.Combine(mockBaseDirectory.Path, "NuGet.Config"), configContents);
                var settings = new Settings(mockBaseDirectory);
                var packageSourceProvider = new PackageSourceProvider(settings);
                var sources = new[] { new PackageSource("one"), new PackageSource("two", "two", isEnabled: false), new PackageSource("three") };

                // Act
                packageSourceProvider.SavePackageSources(sources);

                // Assert
                var packageSourcesSection = settings.GetSection("packageSources");
                packageSourcesSection.Should().NotBeNull();
                packageSourcesSection.Items.Count.Should().Be(3);
                packageSourcesSection.Items.Should().AllBeOfType<SourceItem>();

                var children = packageSourcesSection.Items.Select(c => c as SourceItem).ToList();
                children[0].Key.Should().Be("one");
                children[0].ProtocolVersion.Should().BeNullOrEmpty();
                children[1].Key.Should().Be("two");
                children[1].ProtocolVersion.Should().BeNullOrEmpty();
                children[2].Key.Should().Be("three");
                children[2].ProtocolVersion.Should().BeNullOrEmpty();

                var disabledPackageSourcesSection = settings.GetSection("disabledPackageSources");
                disabledPackageSourcesSection.Should().NotBeNull();
                disabledPackageSourcesSection.Items.Count.Should().Be(1);

                var two = disabledPackageSourcesSection.Items.FirstOrDefault() as AddItem;
                two.Should().NotBeNull();
                two.Key.Should().Be("two");
                two.Value.Should().Be("true");
            }
        }

        [Fact]
        public void SavePackageSources_SavesEncryptedCredentials()
        {
            using (var mockBaseDirectory = TestDirectory.CreateInTemp())
            {
                // Arrange
                var configContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
</configuration>";

                File.WriteAllText(Path.Combine(mockBaseDirectory.Path, "NuGet.Config"), configContents);
                var settings = new Settings(mockBaseDirectory);
                var packageSourceProvider = new PackageSourceProvider(settings);
                var encryptedPassword = Guid.NewGuid().ToString();
                var credentials = new PackageSourceCredential("twoname", "User", encryptedPassword, isPasswordClearText: false);

                var sources = new[]
                {
                    new PackageSource("one"),
                    new PackageSource("http://twosource", "twoname") { Credentials = credentials },
                    new PackageSource("three")
                };

                // Act
                packageSourceProvider.SavePackageSources(sources);

                // Assert
                var packageSourcesSection = settings.GetSection("packageSources");
                packageSourcesSection.Should().NotBeNull();
                packageSourcesSection.Items.Count.Should().Be(3);
                packageSourcesSection.Items.Should().AllBeOfType<SourceItem>();

                var children = packageSourcesSection.Items.Select(c => c as SourceItem).ToList();
                children[0].Key.Should().Be("one");
                children[0].ProtocolVersion.Should().BeNullOrEmpty();
                children[1].Key.Should().Be("twoname");
                children[1].GetValueAsPath().Should().Be("http://twosource");
                children[1].ProtocolVersion.Should().BeNullOrEmpty();
                children[2].Key.Should().Be("three");
                children[2].ProtocolVersion.Should().BeNullOrEmpty();

                var disabledPackageSourcesSection = settings.GetSection("disabledPackageSources");
                disabledPackageSourcesSection.Should().BeNull();

                var sourcesCredentialsSection = settings.GetSection("packageSourceCredentials");
                sourcesCredentialsSection.Should().NotBeNull();
                sourcesCredentialsSection.Items.Count.Should().Be(1);
                var two = sourcesCredentialsSection.Items.FirstOrDefault() as CredentialsItem;
                two.Should().NotBeNull();
                two.ElementName.Should().Be("twoname");
                two.Username.Should().Be("User");
                two.IsPasswordClearText.Should().BeFalse();
                two.Password.Should().Be(encryptedPassword);
            }
        }

        [Fact]
        public void SavePackageSources_SavesClearTextCredentials()
        {
            using (var mockBaseDirectory = TestDirectory.CreateInTemp())
            {
                // Arrange
                var configContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
</configuration>";

                File.WriteAllText(Path.Combine(mockBaseDirectory.Path, "NuGet.Config"), configContents);
                var settings = new Settings(mockBaseDirectory);
                var packageSourceProvider = new PackageSourceProvider(settings);
                var credentials = new PackageSourceCredential("twoname", "User", "password", isPasswordClearText: true);

                var sources = new[]
                {
                    new PackageSource("one"),
                    new PackageSource("http://twosource", "twoname") { Credentials = credentials },
                    new PackageSource("three")
                };

                // Act
                packageSourceProvider.SavePackageSources(sources);

                // Assert
                var packageSourcesSection = settings.GetSection("packageSources");
                packageSourcesSection.Should().NotBeNull();
                packageSourcesSection.Items.Count.Should().Be(3);
                packageSourcesSection.Items.Should().AllBeOfType<SourceItem>();

                var children = packageSourcesSection.Items.Select(c => c as SourceItem).ToList();
                children[0].Key.Should().Be("one");
                children[0].ProtocolVersion.Should().BeNullOrEmpty();
                children[1].Key.Should().Be("twoname");
                children[1].GetValueAsPath().Should().Be("http://twosource");
                children[1].ProtocolVersion.Should().BeNullOrEmpty();
                children[2].Key.Should().Be("three");
                children[2].ProtocolVersion.Should().BeNullOrEmpty();

                var disabledPackageSourcesSection = settings.GetSection("disabledPackageSources");
                disabledPackageSourcesSection.Should().BeNull();

                var sourcesCredentialsSection = settings.GetSection("packageSourceCredentials");
                sourcesCredentialsSection.Should().NotBeNull();
                sourcesCredentialsSection.Items.Count.Should().Be(1);
                var two = sourcesCredentialsSection.Items.FirstOrDefault() as CredentialsItem;
                two.Should().NotBeNull();
                two.ElementName.Should().Be("twoname");
                two.Username.Should().Be("User");
                two.IsPasswordClearText.Should().BeTrue();
                two.Password.Should().Be("password");
            }
        }

        [Fact]
        public void LoadPackageSourcesWithDisabledPackageSourceIsUpperCase()
        {
            // Arrange
            var settings = new Mock<ISettings>(MockBehavior.Strict);
            settings.Setup(s => s.GetSection("packageSources"))
                .Returns(new VirtualSettingSection("packageSources",
                        new SourceItem("one", "onesource"),
                        new SourceItem("TWO", "twosource"),
                        new SourceItem("three", "threesource")
                    ));
            settings.Setup(s => s.GetSection("disabledPackageSources"))
                .Returns(new VirtualSettingSection("disabledPackageSources",
                    new AddItem("TWO", "true")
                    ));
            settings.Setup(s => s.GetSection("packageSourceCredentials"))
                .Returns(new VirtualSettingSection("packageSourceCredentials"));
            settings.Setup(s => s.GetSection("config"))
                .Returns(new VirtualSettingSection("config"));

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
        public void HighPrioritySourceDisabled()
        {
            // Arrange
            using (var mockBaseDirectory = TestDirectory.CreateInTemp())
            {
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
                SettingsTestUtils.CreateConfigurationFile("nuget.config", Path.Combine(mockBaseDirectory, "a", "b"), configContent1);
                SettingsTestUtils.CreateConfigurationFile("nuget.config", Path.Combine(mockBaseDirectory, "a", "b", "c"), configContent2);

                var settings = Settings.LoadSettings(
                    Path.Combine(mockBaseDirectory, "a", "b", "c"),
                    configFileName: null,
                    machineWideSettings: null,
                    loadUserWideSettings: false,
                    useTestingGlobalPath: false);

                var provider = CreatePackageSourceProvider(settings);
                // Act
                var values = provider.LoadPackageSources().ToList();

                // Assert
                Assert.Equal(1, values.Count);
                Assert.False(values[0].IsEnabled);
                Assert.Equal("a", values[0].Name);
                Assert.Equal("http://a", values[0].Source);
            }
        }

        // Test that a source added in a low priority config file is disabled
        // if it's listed in <disabledPackageSources> in a high priority file.
        [Fact]
        public void LowPrioritySourceDisabled()
        {
            // Arrange
            using (var mockBaseDirectory = TestDirectory.CreateInTemp())
            {
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
                SettingsTestUtils.CreateConfigurationFile("nuget.config", Path.Combine(mockBaseDirectory, "a", "b"), configContent2);
                SettingsTestUtils.CreateConfigurationFile("nuget.config", Path.Combine(mockBaseDirectory, "a", "b", "c"), configContent1);

                var settings = Settings.LoadSettings(
                    Path.Combine(mockBaseDirectory, "a", "b", "c"),
                    configFileName: null,
                    machineWideSettings: null,
                    loadUserWideSettings: false,
                    useTestingGlobalPath: false);

                var provider = CreatePackageSourceProvider(settings);

                // Act
                var values = provider.LoadPackageSources().ToList();

                // Assert
                Assert.Equal(1, values.Count);
                Assert.False(values[0].IsEnabled);
                Assert.Equal("a", values[0].Name);
                Assert.Equal("http://a", values[0].Source);
            }
        }

        [Fact]
        public void V2NotDisabled()
        {
            // Arrange
            using (var mockBaseDirectory = TestDirectory.CreateInTemp())
            {
                var configContent = @"<configuration>
    <packageSources>
        <add key='nuget.org' value='https://www.nuget.org/api/v2/' />
    </packageSources>
</configuration>";
                SettingsTestUtils.CreateConfigurationFile("nuget.config", mockBaseDirectory, configContent);

                var settings = Settings.LoadSettings(
                    mockBaseDirectory,
                    configFileName: null,
                    machineWideSettings: null,
                    loadUserWideSettings: false,
                    useTestingGlobalPath: false);

                var provider = CreatePackageSourceProvider(settings);

                // Act
                var values = provider.LoadPackageSources().Where(p => p.Name.Equals("nuget.org", StringComparison.OrdinalIgnoreCase)).ToList();

                // Assert
                Assert.True(values[0].IsEnabled);
            }
        }

        [Fact]
        public void AddPackageSourcesWithConfigFile()
        {

            using (var mockBaseDirectory = TestDirectory.CreateInTemp())
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

                var settings = Settings.LoadSettings(mockBaseDirectory.Path,
                   configFileName: "NuGet.config",
                   machineWideSettings: null,
                   loadUserWideSettings: true,
                   useTestingGlobalPath: false);
                var packageSourceProvider = new PackageSourceProvider(settings);

                // Act
                var sources = packageSourceProvider.LoadPackageSources().ToList();
                sources.Add(new PackageSource("https://test.org", "test"));
                packageSourceProvider.SavePackageSources(sources);

                // Assert
                Assert.Equal(
                      SettingsTestUtils.RemoveWhitespace(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""NuGet.org"" value=""https://NuGet.org"" />
        <add key=""test"" value=""https://test.org"" />
    </packageSources>
</configuration>
"),
                  SettingsTestUtils.RemoveWhitespace(File.ReadAllText(Path.Combine(mockBaseDirectory.Path, "NuGet.config"))));
            }
        }

        [Fact]
        public void SavePackageSources_AddDisabledSourceToTheConfigContainingSource()
        {
            using (var mockBaseDirectory = TestDirectory.CreateInTemp())
            {
                // Arrange
                var config1Contents = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""NuGet.org"" value=""https://NuGet.org"" />
    </packageSources>
</configuration>
";
                File.WriteAllText(Path.Combine(mockBaseDirectory.Path, "NuGet.config"), config1Contents);

                var config2Contents = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
</configuration>";

                var rootPath = Path.Combine(mockBaseDirectory.Path, Path.GetRandomFileName());
                Directory.CreateDirectory(rootPath);
                File.WriteAllText(Path.Combine(rootPath, "NuGet.config"), config2Contents);

                var settings = Settings.LoadSettings(rootPath,
                    configFileName: null,
                    machineWideSettings: null,
                    loadUserWideSettings: false,
                    useTestingGlobalPath: false);
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
                Assert.Equal(SettingsTestUtils.RemoveWhitespace(config2Contents),
                    SettingsTestUtils.RemoveWhitespace(File.ReadAllText(Path.Combine(rootPath, "NuGet.config"))));
                Assert.Equal(
                        SettingsTestUtils.RemoveWhitespace(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""NuGet.org"" value=""https://NuGet.org"" />
    </packageSources>
    <disabledPackageSources>
        <add key=""NuGet.org"" value=""true"" />
    </disabledPackageSources>
</configuration>
"), SettingsTestUtils.RemoveWhitespace(File.ReadAllText(Path.Combine(mockBaseDirectory.Path, "NuGet.config"))));
            }
        }

        [Fact]
        public void SavePackageSources_WritesToTheSettingsFileWithTheNearestPriority()
        {
            using (var mockBaseDirectory = TestDirectory.CreateInTemp())
            {
                // Arrange
                var config1Contents =
                    @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""NuGet.org"" value=""https://NuGet.org"" />
    </packageSources>
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

                var settings = Settings.LoadSettings(rootPath,
                    configFileName: null,
                    machineWideSettings: null,
                    loadUserWideSettings: false,
                    useTestingGlobalPath: false);
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
                Assert.Equal(SettingsTestUtils.RemoveWhitespace(config1Contents),
                    SettingsTestUtils.RemoveWhitespace(File.ReadAllText(Path.Combine(mockBaseDirectory.Path, "NuGet.config"))));

                Assert.Equal(SettingsTestUtils.RemoveWhitespace(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
            <add key=""test.org"" value=""https://test.org"" />
            <add key=""NuGet.org"" value=""https://NuGet.org"" />
    </packageSources>
    <disabledPackageSources>
        <add key=""NuGet.org"" value=""true"" />
    </disabledPackageSources>
</configuration>"), SettingsTestUtils.RemoveWhitespace(File.ReadAllText(Path.Combine(rootPath, "NuGet.config"))));
            }
        }

        [Fact]
        public void SavePackageSources_AddsNewSourcesToTheSettingWithLowestPriority()
        {
            using (var mockBaseDirectory = TestDirectory.CreateInTemp())
            {
                // Arrange
                var config1Contents = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""test.org"" value=""https://test.org"" />
    </packageSources>
</configuration>";

                var config2Contents = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key='NuGet.org' value='https://NuGet.org' />
    </packageSources>
</configuration>";
                File.WriteAllText(Path.Combine(mockBaseDirectory.Path, "NuGet.config"), config1Contents);

                var rootPath = Path.Combine(mockBaseDirectory.Path, Path.GetRandomFileName());
                Directory.CreateDirectory(rootPath);
                File.WriteAllText(Path.Combine(rootPath, "NuGet.config"), config2Contents);

                var settings = Settings.LoadSettings(rootPath,
                    configFileName: null,
                    machineWideSettings: null,
                    loadUserWideSettings: false,
                    useTestingGlobalPath: false);
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
                    }
                    );

                // Act - 2
                sources[1].IsEnabled = false;
                sources.Add(new PackageSource("http://newsource", "NewSourceName"));

                packageSourceProvider.SavePackageSources(sources);

                // Assert - 2
                Assert.Equal(SettingsTestUtils.RemoveWhitespace(@"<?xml version=""1.0"" encoding=""utf-8""?>
                <configuration>
                    <packageSources>
                        <add key=""test.org"" value=""https://test.org"" />
                        <add key=""NewSourceName"" value=""http://newsource"" />
                    </packageSources>
                    <disabledPackageSources>
                        <add key=""test.org"" value=""true"" />
                    </disabledPackageSources>
                </configuration>
                "), SettingsTestUtils.RemoveWhitespace(File.ReadAllText(Path.Combine(mockBaseDirectory.Path, "NuGet.config"))));

                Assert.Equal(SettingsTestUtils.RemoveWhitespace(config2Contents), SettingsTestUtils.RemoveWhitespace(File.ReadAllText(Path.Combine(rootPath, "NuGet.config"))));
            }
        }

        [Fact]
        public void SavePackageSources_AddsOrderingForCollapsedFeeds()
        {
            using (var mockBaseDirectory = TestDirectory.CreateInTemp())
            {
                // Arrange
                var configContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""nuget.org"" value=""https://nuget.org"" />
        <add key=""test.org"" value=""https://test.org"" />
        <add key=""test.org"" value=""https://new.test.org"" protocolVersion=""3"" />
        <add key=""test2"" value=""https://test2.net"" />
    </packageSources>
</configuration>";
                File.WriteAllText(Path.Combine(mockBaseDirectory.Path, "NuGet.config"), configContents);

                var rootPath = Path.Combine(mockBaseDirectory.Path, Path.GetRandomFileName());

                var settings = Settings.LoadSettings(rootPath,
                    configFileName: null,
                    machineWideSettings: null,
                    loadUserWideSettings: false,
                    useTestingGlobalPath: false);
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
                Assert.Equal(SettingsTestUtils.RemoveWhitespace(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""nuget.org"" value=""https://nuget.org"" />
        <add key=""test.org"" value=""https://test.org"" />
        <add key=""test.org"" value=""https://new2.test.org"" protocolVersion=""3"" />
        <add key=""test2"" value=""https://test2.net"" />
    </packageSources>
</configuration>"), SettingsTestUtils.RemoveWhitespace(File.ReadAllText(Path.Combine(mockBaseDirectory.Path, "NuGet.config"))));
            }
        }

        [Fact]
        public void SavePackageSources_DisabledOneMachineWideSource()
        {
            using (var mockBaseDirectory = TestDirectory.CreateInTemp())
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

                var machineWideSetting = new Settings(mockBaseDirectory.Path, "machinewide.config", isMachineWide: true);
                var m = new Mock<IMachineWideSettings>();
                m.SetupGet(obj => obj.Settings).Returns(machineWideSetting);

                var settings = Settings.LoadSettings(mockBaseDirectory.Path,
                   configFileName: null,
                   machineWideSettings: m.Object,
                   loadUserWideSettings: true,
                   useTestingGlobalPath: true);
                var packageSourceProvider = new PackageSourceProvider(settings);
                var sources = packageSourceProvider.LoadPackageSources().ToList();

                // Act
                sources[2].IsEnabled = false;
                packageSourceProvider.SavePackageSources(sources);

                // Assert
                var newSources = packageSourceProvider.LoadPackageSources().ToList();
                Assert.True(newSources[1].IsEnabled);
                Assert.Equal("Microsoft and .NET", newSources[1].Name);

                Assert.False(newSources[2].IsEnabled);
                Assert.Equal("test1", newSources[2].Name);
            }
        }

        [Fact]
        public void DisabledMachineWideSourceByDefaultWithNull()
        {
            using (var mockBaseDirectory = TestDirectory.CreateInTemp())
            {
                // Arrange
                var settings = Settings.LoadSettings(mockBaseDirectory.Path,
                                  configFileName: null,
                                  machineWideSettings: null,
                                  loadUserWideSettings: true,
                                  useTestingGlobalPath: true);
                var packageSourceProvider = new PackageSourceProvider(settings);

                // Act
                var sources = packageSourceProvider.LoadPackageSources().ToList();

                // Assert
                Assert.Equal(1, sources.Count);
            }
        }

        [Fact]
        public void LoadPackageSourceEmptyConfigFileOnUserMachine()
        {
            using (var mockBaseDirectory = TestDirectory.CreateInTemp())
            {
                // Arrange
                var configContents =
                     @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
      <clear />
    </packageSources>
</configuration>
";
                File.WriteAllText(Path.Combine(mockBaseDirectory.Path, "nuget.config"), configContents);
                var settings = Settings.LoadSettings(mockBaseDirectory.Path,
                                  configFileName: null,
                                  machineWideSettings: null,
                                  loadUserWideSettings: true,
                                  useTestingGlobalPath: true);
                var packageSourceProvider = new PackageSourceProvider(settings);

                // Act
                var sources = packageSourceProvider.LoadPackageSources().ToList();

                // Assert
                Assert.Equal(0, sources.Count);
            }
        }

        [Fact]
        public void LoadPackageSourceLocalConfigFileOnUserMachine()
        {
            using (var mockBaseDirectory = TestDirectory.CreateInTemp())
            {
                // Arrange
                var configContents =
                     @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
      <clear />
      <add key=""test"" value=""https://nuget/test"" />
    </packageSources>
</configuration>
";
                File.WriteAllText(Path.Combine(mockBaseDirectory.Path, "nuget.config"), configContents);
                var settings = Settings.LoadSettings(mockBaseDirectory.Path,
                                  configFileName: null,
                                  machineWideSettings: null,
                                  loadUserWideSettings: true,
                                  useTestingGlobalPath: true);
                var packageSourceProvider = new PackageSourceProvider(settings);

                // Act
                var sources = packageSourceProvider.LoadPackageSources().ToList();

                // Assert
                Assert.Equal(1, sources.Count);
                Assert.Equal(@"https://nuget/test", sources[0].Source);
                Assert.Equal("test", sources[0].Name);
            }
        }

        [Fact]

        public void SavePackageSources_IgnoreSettingBeforeClear()
        {
            using (var mockBaseDirectory = TestDirectory.CreateInTemp())
            {
                // Arrange
                var configContents =
                     @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
      <clear />
      <add key=""test"" value=""https://nuget/test"" />
    </packageSources>
</configuration>
";
                File.WriteAllText(Path.Combine(mockBaseDirectory.Path, "nuget.config"), configContents);
                var settings = Settings.LoadSettings(mockBaseDirectory.Path,
                                  configFileName: null,
                                  machineWideSettings: null,
                                  loadUserWideSettings: true,
                                  useTestingGlobalPath: true);
                var packageSourceProvider = new PackageSourceProvider(settings);

                // Act
                var sources = packageSourceProvider.LoadPackageSources().ToList();

                packageSourceProvider.SavePackageSources(sources);

                // Assert
                var text = SettingsTestUtils.RemoveWhitespace(File.ReadAllText(Path.Combine(mockBaseDirectory, "TestingGlobalPath", "NuGet.Config")));
                var result = SettingsTestUtils.RemoveWhitespace(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""nuget.org"" value=""https://api.nuget.org/v3/index.json"" protocolVersion=""3"" />
  </packageSources>
</configuration>");
                Assert.Equal(result, text);
            }
        }

        [Fact]
        public void SavePackageSources_ThrowWhenConfigReadOnly()
        {
            using (var mockBaseDirectory = TestDirectory.CreateInTemp())
            {
                // Arrange
                var configContents =
                     @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
      <clear />
      <add key=""test"" value=""https://nuget/test"" />
    </packageSources>
</configuration>
";

                File.WriteAllText(Path.Combine(mockBaseDirectory.Path, "NuGet.Config"), configContents);
                File.SetAttributes(Path.Combine(mockBaseDirectory.Path, "NuGet.Config"), FileAttributes.ReadOnly);

                var settings = Settings.LoadSettings(mockBaseDirectory.Path,
                                  configFileName: null,
                                  machineWideSettings: null,
                                  loadUserWideSettings: true,
                                  useTestingGlobalPath: true);
                var packageSourceProvider = new PackageSourceProvider(settings);

                // Act
                var sources = packageSourceProvider.LoadPackageSources().ToList();

                sources.Add(new PackageSource("https://test3.net", "test3"));

                var ex = Assert.Throws<NuGetConfigurationException>(() => packageSourceProvider.SavePackageSources(sources));

                // Assert
                var path = Path.Combine(mockBaseDirectory, "NuGet.Config");
                Assert.Equal($"Failed to read NuGet.Config due to unauthorized access. Path: '{path}'.", ex.Message);
            }
        }

        [Fact]
        public void DefaultPushSourceInNuGetConfig()
        {
            using (var mockBaseDirectory = TestDirectory.CreateInTemp())
            {
                // Arrange
                var configContentsWithDefault =
@"<?xml version='1.0'?>
<configuration>
    <config>
        <add key='DefaultPushSource' value='\\myshare\packages' />
    </config>
    <packageSources>
        <add key='NuGet.org' value='https://NuGet.org' />
    </packageSources>
</configuration>";
                var configContentWithoutDefault = configContentsWithDefault.Replace("DefaultPushSource", "WithoutDefaultPushSource");

                File.WriteAllText(Path.Combine(mockBaseDirectory.Path, "WithDefaultPushSource.config"), configContentsWithDefault);
                File.WriteAllText(Path.Combine(mockBaseDirectory.Path, "WithoutDefaultPushSource.config"), configContentWithoutDefault);

                var settingsWithDefault = Settings.LoadSettings(mockBaseDirectory.Path,
                   configFileName: "WithDefaultPushSource.config",
                   machineWideSettings: null,
                   loadUserWideSettings: true,
                   useTestingGlobalPath: false);

                var settingsWithoutDefault = Settings.LoadSettings(mockBaseDirectory.Path,
                   configFileName: "WithoutDefaultPushSource.config",
                   machineWideSettings: null,
                   loadUserWideSettings: true,
                   useTestingGlobalPath: false);

                var packageSourceProviderWithDefault = new PackageSourceProvider(settingsWithDefault);
                var packageSourceProviderWithoutDefault = new PackageSourceProvider(settingsWithoutDefault);

                // Act
                var defaultPushSourceWithDefault = packageSourceProviderWithDefault.DefaultPushSource;
                var defaultPushSourceWithoutDefault = packageSourceProviderWithoutDefault.DefaultPushSource;

                // Assert
                Assert.Equal(@"\\myshare\packages", defaultPushSourceWithDefault);
                Assert.True(string.IsNullOrEmpty(defaultPushSourceWithoutDefault));
            }
        }

        [Fact]
        public void LoadPackageSources_DoesNotDecryptPassword()
        {
            using (var mockBaseDirectory = TestDirectory.CreateInTemp())
            {
                // Arrange
                var configContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
      <clear />
      <add key=""test"" value=""https://nuget/test"" />
    </packageSources>
<packageSourceCredentials>
    <test>
      <add key='Username' value='myusername' />
      <add key='Password' value='random-encrypted-password' />
    </test>
  </packageSourceCredentials>
</configuration>";

                File.WriteAllText(Path.Combine(mockBaseDirectory.Path, "NuGet.Config"), configContents);
                var settings = Settings.LoadSettings(mockBaseDirectory.Path,
                                  configFileName: null,
                                  machineWideSettings: null,
                                  loadUserWideSettings: false,
                                  useTestingGlobalPath: false);
                var packageSourceProvider = new PackageSourceProvider(settings);

                // Act
                var sources = packageSourceProvider.LoadPackageSources().ToList();

                // Assert
                Assert.Equal(1, sources.Count);
                Assert.Equal("test", sources[0].Name);
                Assert.Equal("https://nuget/test", sources[0].Source);
                AssertCredentials(sources[0].Credentials, "test", "myusername", "random-encrypted-password", isPasswordClearText: false);
            }
        }

        [Fact]
        public void LoadPackageSources_DoesNotLoadClearedSource()
        {
            using (var mockBaseDirectory = TestDirectory.CreateInTemp())
            {
                // Arrange
                var configContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
      <add key=""test"" value=""https://nuget/test"" />
    </packageSources>
<packageSourceCredentials>
    <test>
      <add key='Username' value='myusername' />
      <add key='Password' value='removed' />
    </test>
  </packageSourceCredentials>
</configuration>
";
                var configContents1 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
      <clear />
      <add key=""test2"" value=""https://nuget/test2"" />
    </packageSources>
</configuration>
";
                SettingsTestUtils.CreateConfigurationFile(
                    "nuget.config",
                    Path.Combine(mockBaseDirectory, "TestingGlobalPath"),
                    configContents);
                File.WriteAllText(Path.Combine(mockBaseDirectory.Path, "NuGet.Config"), configContents1);
                var settings = Settings.LoadSettings(
                    mockBaseDirectory.Path,
                    configFileName: null,
                    machineWideSettings: null,
                    loadUserWideSettings: true,
                    useTestingGlobalPath: true);
                var packageSourceProvider = new PackageSourceProvider(settings);

                // Act
                var sources = packageSourceProvider.LoadPackageSources().ToList();

                // Assert
                Assert.Equal(1, sources.Count);
                Assert.Equal("test2", sources[0].Name);
                Assert.Equal("https://nuget/test2", sources[0].Source);
                Assert.Null(sources[0].Credentials);
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
            var toVerifyList = new List<PackageSource>();
            toVerifyList = psp.LoadPackageSources().ToList();

            Assert.Equal(toVerifyList.Count, count);
            var index = 0;
            foreach (var ps in toVerifyList)
            {
                Assert.Equal(ps.Name, names[index]);
                Assert.Equal(ps.Source, feeds[index]);
                Assert.Equal(ps.IsEnabled, isEnabled[index]);
                Assert.Equal(ps.IsOfficial, isOfficial[index]);
                index++;
            }
        }

        private PackageSourceProvider CreatePackageSourceProvider(
            ISettings settings = null)
        {
            settings = settings ?? new Mock<ISettings>().Object;
            return new PackageSourceProvider(settings);
        }

        private void AssertPackageSource(PackageSource ps, string name, string source, bool isEnabled, bool isMachineWide = false, bool isOfficial = false)
        {
            Assert.Equal(name, ps.Name);
            Assert.Equal(source, ps.Source);
            Assert.True(ps.IsEnabled == isEnabled);
            Assert.True(ps.IsMachineWide == isMachineWide);
            Assert.True(ps.IsOfficial == isOfficial);
        }

        private static void AssertKeyValuePair(string expectedKey, string expectedValue, KeyValuePair<string, string> actual)
        {
            Assert.NotNull(actual);
            Assert.Equal(expectedKey, actual.Key);
            Assert.Equal(expectedValue, actual.Value);
        }

        private void AssertCredentials(PackageSourceCredential actual, string source, string userName, string passwordText, bool isPasswordClearText = true)
        {
            Assert.NotNull(actual);
            Assert.Equal(source, actual.Source);
            Assert.Equal(userName, actual.Username);
            Assert.Equal(passwordText, actual.PasswordText);
            Assert.Equal(isPasswordClearText, actual.IsPasswordClearText);
        }
    }
}
