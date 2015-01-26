using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Configuration;
using Xunit;
using Xunit.Extensions;
using Xunit.Sdk;

namespace NuGet.Configuration.Test
{
    public class PackageServiceProviderTests
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

            string enabledReplacement = @"<add key='nuget.org(v3)' value='https://api.nuget.org/v3/index.json/' />";
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
            string enabledReplacement = @"<add key='nuget.org(v3)' value='"+ randomURL + "' />";
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
            string enabledReplacement = @"<add key='nuget.org(v3)' value='https://api.nuget.org/v3/index.json/' />";
            enabledReplacement = enabledReplacement + @"<add key='nuget.org' value='" + randomURL + "' />";
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

            string enabledReplacement = @"<add key='anotherName' value='https://api.nuget.org/v3/index.json/' />";
            enabledReplacement = enabledReplacement + @"<add key='nuget.org' value='https://www.nuget.org/api/v2/' />";
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

            string enabledReplacement = @"<add key='nuget.org(v3)' value='https://api.nuget.org/v3/index.json/' />";
            enabledReplacement = enabledReplacement + @"<add key='anotherName' value='https://www.nuget.org/api/v2/' />";
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

            string enabledReplacement = @"<add key='nuget.org' value='https://www.nuget.org/api/v2/' />";
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

        private string CreateNuGetConfigContent(string enabledReplacement = "", string disabledReplacement = "")
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
            nugetConfigBaseString.AppendLine(@"<add key='All' value='(Aggregate source)' />");
            nugetConfigBaseString.AppendLine("</activePackageSource>");
            nugetConfigBaseString.AppendLine("</configuration>");

            string nugetConfig = nugetConfigBaseString.ToString();
            nugetConfig = nugetConfig.Replace("[EnabledSources]", enabledReplacement);
            nugetConfig = nugetConfig.Replace("[DisabledSources]", disabledReplacement);
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
