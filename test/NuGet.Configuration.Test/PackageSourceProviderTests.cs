using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NuGet.Configuration.Test
{
    public class PackageSourceProviderTests
    {
        [Fact]
        public void PrimaryAndSecondaryAreAddedWhenNotPresent()
        {
            // Act
            NullSettings settings = new NullSettings();
            List<PackageSource> primary = new List<PackageSource>();
            PackageSource item = new PackageSource(NuGetConstants.V3FeedUrl, NuGetConstants.V3FeedName);
            primary.Add(item);

            List<PackageSource> secondary = new List<PackageSource>();
            PackageSource item2 = new PackageSource(NuGetConstants.V2FeedUrl, NuGetConstants.V2FeedName, false);
            secondary.Add(item2);

            PackageSourceProvider psp = new PackageSourceProvider(settings, primary, secondary);

            // Assert
            //Primary IsEnabled = true, IsOfficial = true (Added for the first time)
            //Secondary IsEnabled = false, IsOfficial = true  (Added for the first time)
            VerifyPackageSource(psp, 2, new string[] { NuGetConstants.V3FeedName, NuGetConstants.V2FeedName },
                new string[] { NuGetConstants.V3FeedUrl, NuGetConstants.V2FeedUrl },
                new bool[] { true, false }, new bool[] { true, true });
        }

        [Fact]
        public void SecondaryIsAddedWhenNotPresentButDisabled()
        {
            // Act
            //Create nuget.config that has Primary defined and Secondary missing
            string nugetConfigFileFolder = NuGet.Configuration.Test.TestFilesystemUtility.CreateRandomTestFolder();
            string nugetConfigFilePath = Path.Combine(nugetConfigFileFolder, "nuget.config");
            File.Create(nugetConfigFilePath).Close();

            string enabledReplacement = @"<add key='" + NuGetConstants.V3FeedName + "' value='" + NuGetConstants.V3FeedUrl + "' />";
            string disabledReplacement = string.Empty;
            File.WriteAllText(nugetConfigFilePath, CreateNuGetConfigContent(enabledReplacement, disabledReplacement));

            Settings settings = new Settings(nugetConfigFileFolder, "nuget.config");
            PackageSourceProvider before = new PackageSourceProvider(settings);
            VerifyPackageSource(before, 1, new string[] { NuGetConstants.V3FeedName },
                new string[] { NuGetConstants.V3FeedUrl},
                new bool[] { true }, new bool[] { false });

            List<PackageSource> primary = new List<PackageSource>();
            PackageSource item = new PackageSource(NuGetConstants.V3FeedUrl, NuGetConstants.V3FeedName);
            primary.Add(item);

            List<PackageSource> secondary = new List<PackageSource>();
            PackageSource item2 = new PackageSource(NuGetConstants.V2FeedUrl, NuGetConstants.V2FeedName, false);
            secondary.Add(item2);
                                  
            PackageSourceProvider after = new PackageSourceProvider(settings, primary, secondary);
            
            // Assert
            //Primary already exists in nuget.config as Enabled. So IsEnabled = true. IsOfficial is also set to true when loading package sources
            //Primary IsEnabled = true, IsOfficial = true
            //Secondary is added, marked as official but added as disabled
            //Secondary IsEnabled = false, IsOfficial = true
            VerifyPackageSource(after, 2, new string[] { NuGetConstants.V3FeedName, NuGetConstants.V2FeedName },
                    new string[] { NuGetConstants.V3FeedUrl, NuGetConstants.V2FeedUrl },
                    new bool[] { true, false }, new bool[] { true, true });

            //Clean up
            NuGet.Configuration.Test.TestFilesystemUtility.DeleteRandomTestFolders(nugetConfigFileFolder);
        }

        [Fact]
        public void PrimaryURLIsForcedWhenPrimaryNameHasAnotherFeed()
        {
            // Act
            //Create nuget.config that has Primary name used for a different feed
            string nugetConfigFileFolder = NuGet.Configuration.Test.TestFilesystemUtility.CreateRandomTestFolder();
            string nugetConfigFilePath = Path.Combine(nugetConfigFileFolder, "nuget.config");
            File.Create(nugetConfigFilePath).Close();

            string randomURL = "https://www.somerandomURL.com/";
            string enabledReplacement = @"<add key='" + NuGetConstants.V3FeedName + "' value='"+ randomURL + "' />";
            string disabledReplacement = string.Empty;
            File.WriteAllText(nugetConfigFilePath, CreateNuGetConfigContent(enabledReplacement, disabledReplacement));

            Settings settings = new Settings(nugetConfigFileFolder, "nuget.config");
            PackageSourceProvider before = new PackageSourceProvider(settings);
            VerifyPackageSource(before, 1, new string[] { NuGetConstants.V3FeedName },
                            new string[] { randomURL },
                            new bool[] { true }, new bool[] { false });

            List<PackageSource> primary = new List<PackageSource>();
            PackageSource item = new PackageSource(NuGetConstants.V3FeedUrl, NuGetConstants.V3FeedName);
            primary.Add(item);

            PackageSourceProvider after = new PackageSourceProvider(settings, primary, null);

            // Assert
            //Primary Name already exists in nuget.config but with different URL
            //It gets overwritten by primary package source (which is enabled above while creating it)
            //IsEnabled = true, IsOfficial = true
            VerifyPackageSource(after, 1, new string[] { NuGetConstants.V3FeedName },
                                new string[] { NuGetConstants.V3FeedUrl },
                                new bool[] { true}, new bool[] { true});

            //Clean up
            NuGet.Configuration.Test.TestFilesystemUtility.DeleteRandomTestFolders(nugetConfigFileFolder);
        }

        [Fact]
        public void SecondaryURLIsForcedWhenSecondaryNameHasAnotherFeed()
        {
            // Act
            //Create nuget.config that has Secondary name used for a different feed
            string nugetConfigFileFolder = NuGet.Configuration.Test.TestFilesystemUtility.CreateRandomTestFolder();
            string nugetConfigFilePath = Path.Combine(nugetConfigFileFolder, "nuget.config");
            File.Create(nugetConfigFilePath).Close();
            

            string randomURL = "https://www.somerandomURL.com/";
            string enabledReplacement = @"<add key='" + NuGetConstants.V3FeedName + "' value='" + NuGetConstants.V3FeedUrl + "' />";
            enabledReplacement = enabledReplacement + @"<add key='" + NuGetConstants.V2FeedName + "' value='" + randomURL + "' />";
            string disabledReplacement = string.Empty;
            File.WriteAllText(nugetConfigFilePath, CreateNuGetConfigContent(enabledReplacement, disabledReplacement));

            Settings settings = new Settings(nugetConfigFileFolder, "nuget.config");
            PackageSourceProvider before = new PackageSourceProvider(settings);
            VerifyPackageSource(before, 2, new string[] { NuGetConstants.V3FeedName, NuGetConstants.V2FeedName },
                            new string[] { NuGetConstants.V3FeedUrl, randomURL },
                            new bool[] { true, true }, new bool[] { false, false });

            List<PackageSource> primary = new List<PackageSource>();
            PackageSource item = new PackageSource(NuGetConstants.V3FeedUrl, NuGetConstants.V3FeedName);
            primary.Add(item);

            List<PackageSource> secondary = new List<PackageSource>();
            PackageSource item2 = new PackageSource(NuGetConstants.V2FeedUrl, NuGetConstants.V2FeedName, false);
            secondary.Add(item2);

            PackageSourceProvider after = new PackageSourceProvider(settings, primary, secondary);

            // Assert
            //Seconday name already exists in nuget.config but with different URL
            //It gets overwritten by secondary scource which is disabled (while getting created above)
            //IsOfficial is set to true
            VerifyPackageSource(after, 2, new string[] { NuGetConstants.V3FeedName, NuGetConstants.V2FeedName },
                                new string[] { NuGetConstants.V3FeedUrl, NuGetConstants.V2FeedUrl },
                                new bool[] { true, false }, new bool[] { true, true });

            //Clean up
            NuGet.Configuration.Test.TestFilesystemUtility.DeleteRandomTestFolders(nugetConfigFileFolder);
        }
        
        [Fact]
        public void PrimaryNameNotChangedWhenTheFeedHasAnotherName()
        {
            // Act
            //Create nuget.config that has Primary defined (Feed Name is different) and Secondary defined 
            string nugetConfigFileFolder = NuGet.Configuration.Test.TestFilesystemUtility.CreateRandomTestFolder();
            string nugetConfigFilePath = Path.Combine(nugetConfigFileFolder, "nuget.config");
            File.Create(nugetConfigFilePath).Close();

            string enabledReplacement = @"<add key='anotherName' value='" + NuGetConstants.V3FeedUrl + "' />";
            enabledReplacement = enabledReplacement + @"<add key='" + NuGetConstants.V2FeedName + "' value='" + NuGetConstants.V2FeedUrl + "' />";
            string disabledReplacement = string.Empty;
            File.WriteAllText(nugetConfigFilePath, CreateNuGetConfigContent(enabledReplacement, disabledReplacement));
            
            Settings settings = new Settings(nugetConfigFileFolder, "nuget.config");
            PackageSourceProvider before = new PackageSourceProvider(settings);
            VerifyPackageSource(before, 2, new string[] { "anotherName", NuGetConstants.V2FeedName },
                            new string[] { NuGetConstants.V3FeedUrl, NuGetConstants.V2FeedUrl },
                            new bool[] { true, true }, new bool[] { false, false });

            List<PackageSource> primary = new List<PackageSource>();
            PackageSource item = new PackageSource(NuGetConstants.V3FeedUrl, NuGetConstants.V3FeedName);
            primary.Add(item);

            List<PackageSource> secondary = new List<PackageSource>();
            PackageSource item2 = new PackageSource(NuGetConstants.V2FeedUrl, NuGetConstants.V2FeedName, false);
            secondary.Add(item2);

            PackageSourceProvider after = new PackageSourceProvider(settings, primary, secondary);

            // Assert
            //Primary feed is present in nuget.config but with a different name
            //In this case, we don't set IsOfficial = true
            //Secondary matches both name and URL so secondary is set to true
            //Since this is not the first time primary is getting added, we aren't aggressive in demoting secondary from enabled to disabled
            VerifyPackageSource(after, 2, new string[] { "anotherName", NuGetConstants.V2FeedName },
                                new string[] { NuGetConstants.V3FeedUrl, NuGetConstants.V2FeedUrl },
                                new bool[] { true, true }, new bool[] { false, true });

            //Clean up
            NuGet.Configuration.Test.TestFilesystemUtility.DeleteRandomTestFolders(nugetConfigFileFolder);
        }

        [Fact]
        public void SecondaryNameNotChangedWhenTheFeedHasAnotherName()
        {
            // Act
            //Create nuget.config that has Primary defined and Secondary missing
            string nugetConfigFileFolder = NuGet.Configuration.Test.TestFilesystemUtility.CreateRandomTestFolder();
            string nugetConfigFilePath = Path.Combine(nugetConfigFileFolder, "nuget.config");
            File.Create(nugetConfigFilePath).Close();

            string enabledReplacement = @"<add key='" + NuGetConstants.V3FeedName + "' value='" + NuGetConstants.V3FeedUrl + "' />";
            enabledReplacement = enabledReplacement + @"<add key='anotherName' value='" + NuGetConstants.V2FeedUrl + "' />";
            string disabledReplacement = string.Empty;
            File.WriteAllText(nugetConfigFilePath, CreateNuGetConfigContent(enabledReplacement, disabledReplacement));

            Settings settings = new Settings(nugetConfigFileFolder, "nuget.config");
            PackageSourceProvider before = new PackageSourceProvider(settings);
            VerifyPackageSource(before, 2, new string[] { NuGetConstants.V3FeedName, "anotherName" },
                            new string[] { NuGetConstants.V3FeedUrl, NuGetConstants.V2FeedUrl },
                            new bool[] { true, true }, new bool[] { false, false });

            List<PackageSource> primary = new List<PackageSource>();
            PackageSource item = new PackageSource(NuGetConstants.V3FeedUrl, NuGetConstants.V3FeedName);
            primary.Add(item);

            List<PackageSource> secondary = new List<PackageSource>();
            PackageSource item2 = new PackageSource(NuGetConstants.V2FeedUrl, NuGetConstants.V2FeedName, false);
            secondary.Add(item2);

            PackageSourceProvider after = new PackageSourceProvider(settings, primary, secondary);

            // Assert
            //Secondary feed is present in nuget.config but with a different name
            //In this case, we don't set IsOfficial = true
            //Primary matches both name and URL so primary's IsOfficial is set to true
            //Since this is not the first time primary is getting added, we aren't aggressive in demoting secondary from enabled to disabled
            VerifyPackageSource(after, 2, new string[] { NuGetConstants.V3FeedName, "anotherName"},
                                new string[] { NuGetConstants.V3FeedUrl, NuGetConstants.V2FeedUrl },
                                new bool[] { true, true }, new bool[] { true, false });

            //Clean up
            NuGet.Configuration.Test.TestFilesystemUtility.DeleteRandomTestFolders(nugetConfigFileFolder);
        }

        [Fact]
        public void PrimaryIsEnabledAndSecondaryIsDisabledWhenPrimaryIsAddedForTheFirstTimeAndSecondaryAlreadyExists()
        {
            // Act
            //Create nuget.config that has Secondary defined
            string nugetConfigFileFolder = NuGet.Configuration.Test.TestFilesystemUtility.CreateRandomTestFolder();
            string nugetConfigFilePath = Path.Combine(nugetConfigFileFolder, "nuget.Config");
            File.Create(nugetConfigFilePath).Close();

            string enabledReplacement = @"<add key='" + NuGetConstants.V2FeedName + "' value='" + NuGetConstants.V2FeedUrl + "' />";
            string disabledReplacement = string.Empty;
            File.WriteAllText(nugetConfigFilePath, CreateNuGetConfigContent(enabledReplacement, disabledReplacement));

            Settings settings = new Settings(nugetConfigFileFolder, "nuget.config");
            PackageSourceProvider before = new PackageSourceProvider(settings);
            VerifyPackageSource(before, 1, new string[] { NuGetConstants.V2FeedName },
                            new string[] { NuGetConstants.V2FeedUrl },
                            new bool[] { true }, new bool[] { false });

            List<PackageSource> primary = new List<PackageSource>();
            PackageSource item = new PackageSource(NuGetConstants.V3FeedUrl, NuGetConstants.V3FeedName);
            primary.Add(item);

            List<PackageSource> secondary = new List<PackageSource>();
            PackageSource item2 = new PackageSource(NuGetConstants.V2FeedUrl, NuGetConstants.V2FeedName, false);
            secondary.Add(item2);

            PackageSourceProvider after = new PackageSourceProvider(settings, primary, secondary);

            // Assert
            //First time Primary is getting added so it is set to Enabled
            //Secondary is demoted to disabled even though it is already enabled through nuget.config
            VerifyPackageSource(after, 2, new string[] { NuGetConstants.V2FeedName, NuGetConstants.V3FeedName },
                                new string[] { NuGetConstants.V2FeedUrl, NuGetConstants.V3FeedUrl },
                                new bool[] { false, true }, new bool[] { true, true });

            //Clean up
            NuGet.Configuration.Test.TestFilesystemUtility.DeleteRandomTestFolders(nugetConfigFileFolder);
        }

        [Fact]
        public void ActivePackageSourceCanBeReadAndWrittenInNuGetConfig()
        {
            // Act
            //Create nuget.config that has active package source defined
            string nugetConfigFileFolder = NuGet.Configuration.Test.TestFilesystemUtility.CreateRandomTestFolder();
            string nugetConfigFilePath = Path.Combine(nugetConfigFileFolder, "nuget.Config");
            File.Create(nugetConfigFilePath).Close();

            string enabledReplacement = @"<add key='" + NuGetConstants.V2FeedName + "' value='" + NuGetConstants.V2FeedUrl + "' />";
            string disabledReplacement = string.Empty;
            string activeReplacement = @"<add key='" + NuGetConstants.V2FeedName + "' value='" + NuGetConstants.V2FeedUrl + "' />";
            File.WriteAllText(nugetConfigFilePath, CreateNuGetConfigContent(enabledReplacement, disabledReplacement, activeReplacement));

            Settings settings = new Settings(nugetConfigFileFolder, "nuget.config");
            PackageSourceProvider before = new PackageSourceProvider(settings);
            Assert.Equal(NuGetConstants.V2FeedName, before.ActivePackageSourceName);

            before.SaveActivePackageSource(new PackageSource(NuGetConstants.V3FeedUrl, NuGetConstants.V3FeedName));
            Assert.Equal(NuGetConstants.V3FeedName, before.ActivePackageSourceName);

            //Clean up
            NuGet.Configuration.Test.TestFilesystemUtility.DeleteRandomTestFolders(nugetConfigFileFolder);
        }

        [Fact]
        public void ActivePackageSourceReturnsNullIfNotSetInNuGetConfig()
        {
            // Act
            //Create nuget.config that has active package source defined
            string nugetConfigFileFolder = NuGet.Configuration.Test.TestFilesystemUtility.CreateRandomTestFolder();
            string nugetConfigFilePath = Path.Combine(nugetConfigFileFolder, "nuget.Config");
            File.Create(nugetConfigFilePath).Close();

            string enabledReplacement = @"<add key='" + NuGetConstants.V2FeedName + "' value='" + NuGetConstants.V2FeedUrl + "' />";
            File.WriteAllText(nugetConfigFilePath, CreateNuGetConfigContent(enabledReplacement));

            Settings settings = new Settings(nugetConfigFileFolder, "nuget.config");
            PackageSourceProvider before = new PackageSourceProvider(settings);
            Assert.Null(before.ActivePackageSourceName);

            before.SaveActivePackageSource(new PackageSource(NuGetConstants.V3FeedUrl, NuGetConstants.V3FeedName));
            Assert.Equal(NuGetConstants.V3FeedName, before.ActivePackageSourceName);

            //Clean up
            NuGet.Configuration.Test.TestFilesystemUtility.DeleteRandomTestFolders(nugetConfigFileFolder);
        }

        [Fact]
        public void ActivePackageSourceReturnsNullIfNotPresentInNuGetConfig()
        {
            // Act
            //Create nuget.config that has active package source defined
            string nugetConfigFileFolder = NuGet.Configuration.Test.TestFilesystemUtility.CreateRandomTestFolder();
            string nugetConfigFilePath = Path.Combine(nugetConfigFileFolder, "nuget.Config");
            File.Create(nugetConfigFilePath).Close();

            string enabledReplacement = @"<add key='" + NuGetConstants.V2FeedName + "' value='" + NuGetConstants.V2FeedUrl + "' />";
            string fileContents = CreateNuGetConfigContent(enabledReplacement);
            fileContents = fileContents.Replace("<activePackageSource>", string.Empty);
            fileContents = fileContents.Replace("</activePackageSource>", string.Empty);
            File.WriteAllText(nugetConfigFilePath, fileContents);

            Settings settings = new Settings(nugetConfigFileFolder, "nuget.config");
            PackageSourceProvider before = new PackageSourceProvider(settings);
            Assert.Null(before.ActivePackageSourceName);

            SettingValue newActiveValue = new SettingValue(NuGetConstants.V3FeedName, NuGetConstants.V3FeedUrl, false);
            before.SaveActivePackageSource(new PackageSource(NuGetConstants.V3FeedUrl, NuGetConstants.V3FeedName));
            Assert.Equal(NuGetConstants.V3FeedName, before.ActivePackageSourceName);

            //Clean up
            NuGet.Configuration.Test.TestFilesystemUtility.DeleteRandomTestFolders(nugetConfigFileFolder);
        }

        [Fact]
        public async Task LoadPackageSourcesWithCredentials()
        {
            // Arrange
            //Create nuget.config that has active package source defined
            string nugetConfigFileFolder = NuGet.Configuration.Test.TestFilesystemUtility.CreateRandomTestFolder();
            string nugetConfigFilePath = Path.Combine(nugetConfigFileFolder, "nuget.Config");

            var configContent = @"<?xml version='1.0' encoding='utf-8'?>
<configuration>
  <apikeys>
    <add key='https://www.nuget.org' value='removed' />
    <add key='https://www.myget.org/F/somecompanyfeed-unstable/' value='removed' />
    <add key='https://www.myget.org/F/somecompanyfeed/' value='removed' />
    <add key='https://www.myget.org/F/somecompanyfeed-unstable/api/v2/package' value='removed' />
    <add key='https://www.myget.org/F/somecompanyfeed/api/v2/package' value='removed' />
    <add key='https://www.myget.org/F/somecompanyfeed-unstable/api/v2/' value='removed' />
    <add key='http://nuget.gw.symbolsource.org/Public/NuGet' value='removed' />
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

        private string CreateNuGetConfigContent(string enabledReplacement = "", string disabledReplacement = "", string activeSourceReplacement = "")
        {
            StringBuilder nugetConfigBaseString = new StringBuilder();
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

            string nugetConfig = nugetConfigBaseString.ToString();
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
            int index = 0;
            foreach (PackageSource ps in toVerifyList)
            {
                Assert.Equal(ps.Name, names[index]);
                Assert.Equal(ps.Source, feeds[index]);
                Assert.Equal(ps.IsEnabled, isEnabled[index]);
                Assert.Equal(ps.IsOfficial, isOfficial[index]);
                index++;
            }
        }
    }
}
