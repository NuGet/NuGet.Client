// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.Sdk.TestFramework;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Utilities.UnifiedSettings;
using Moq;
using NuGet.Configuration;
using NuGet.PackageManagement.VisualStudio.Options;
using NuGet.VisualStudio;
using Xunit;

namespace NuGet.PackageManagement.VisualStudio.Test.Options
{
    [Collection(MockedVS.Collection)]
    public class PackageSourceMappingPageTests : NuGetExternalSettingsProviderTests<PackageSourceMappingPage>
    {
        private IEnumerable<PackageSource> _packageSources;

        public PackageSourceMappingPageTests(GlobalServiceProvider sp)
        {
            sp.Reset();
            NuGetUIThreadHelper.SetCustomJoinableTaskFactory(ThreadHelper.JoinableTaskFactory);
            _packageSources = Enumerable.Empty<PackageSource>();
        }

        protected override PackageSourceMappingPage CreateInstance(VSSettings vsSettings)
        {
            Mock<IPackageSourceProvider> mockedPackageSourceProvider = new Mock<IPackageSourceProvider>();
            mockedPackageSourceProvider.Setup(packageSourceProvider => packageSourceProvider.LoadPackageSources())
                .Returns(_packageSources);

            PackageSourceMappingProvider sourceMappingProvider = new(vsSettings);
            return new PackageSourceMappingPage(vsSettings, mockedPackageSourceProvider.Object, sourceMappingProvider);
        }

        [Fact]
        public void VsSettings_SettingsChanged_RaisesEnumSettingChoicesChanged()
        {
            // Arrange
            bool wasEnumSettingChoicesChangedRaised = false;
            PackageSourceMappingPage instance = CreateInstance(_vsSettings);
            instance.EnumSettingChoicesChanged += (s, e) =>
            {
                wasEnumSettingChoicesChangedRaised = true;
            };

            // Act
            instance.VsSettings_SettingsChanged(this, EventArgs.Empty);

            // Assert
            wasEnumSettingChoicesChangedRaised.Should().BeTrue();
        }

        [Fact]
        public async Task SetValueAsync_PreviousMappingsToNonexistantSources_AddNewMapping_ExistingMappingsAreUnchangedAsync()
        {
            // Arrange
            string addNewPackageSourceName4 = "unitTestingSourceName4";
            string packageIdPattern = "Contoso.*";

            var unitTestingSourceMapping1 = new PackageSourceMappingSourceItem("unitTestingSourceName1", [new PackagePatternItem(packageIdPattern)]);
            var nonExistantSourceInSourceMapping2 = new PackageSourceMappingSourceItem("nonExistantSourceName2", [new PackagePatternItem(packageIdPattern)]);
            var unitTestingSourceMapping3 = new PackageSourceMappingSourceItem("unitTestingSourceName3", [new PackagePatternItem(packageIdPattern)]);

            _vsSettings.AddOrUpdate(ConfigurationConstants.PackageSourceMapping, unitTestingSourceMapping1);
            _vsSettings.AddOrUpdate(ConfigurationConstants.PackageSourceMapping, nonExistantSourceInSourceMapping2);
            _vsSettings.AddOrUpdate(ConfigurationConstants.PackageSourceMapping, unitTestingSourceMapping3);

            PackageSourceMappingPage instance = CreateInstance(_vsSettings);

            string sourceName1 = "unitTestingSourceName1";
            string sourceUrl1 = "https://testsource1.com";

            // nonExistantSourceName2 is not added here intentionally.

            string sourceName3 = "unitTestingSourceName3";
            string sourceUrl3 = "https://testsource3.com";

            _packageSources =
            [
                new PackageSource(sourceUrl1, sourceName1, isEnabled: true),
                // nonExistantSourceName is not added here intentionally.
                new PackageSource(sourceUrl3, sourceName3, isEnabled: true)
            ];

            // Configure Unified Settings input to modify an existing package source mapping.

            // Unchanged package source mapping.
            Dictionary<string, object> packageSourceDictionary1 = new Dictionary<string, object>();
            packageSourceDictionary1[PackageSourceMappingPage.MonikerPackageId] = unitTestingSourceMapping1.Patterns.Single().Pattern;
            packageSourceDictionary1[PackageSourceMappingPage.MonikerSourceNames] = new List<string>() { unitTestingSourceMapping1.Key };

            // nonExistantSourceName is not known to Unified Settings, so it is not part of the request to SetValue.

            // Unchanged package source mapping.
            Dictionary<string, object> packageSourceDictionary3 = new Dictionary<string, object>();
            packageSourceDictionary3[PackageSourceMappingPage.MonikerPackageId] = unitTestingSourceMapping3.Patterns.Single().Pattern;
            packageSourceDictionary3[PackageSourceMappingPage.MonikerSourceNames] = new List<string>() { unitTestingSourceMapping3.Key };

            // Add a new package source mapping for another existing source.
            Dictionary<string, object> packageSourceDictionary4 = new Dictionary<string, object>();
            packageSourceDictionary4[PackageSourceMappingPage.MonikerPackageId] = packageIdPattern;
            packageSourceDictionary4[PackageSourceMappingPage.MonikerSourceNames] = new List<string>() { addNewPackageSourceName4 };

            IList<IDictionary<string, object>> sourceMappingDictionaryList =
                new List<IDictionary<string, object>>(capacity: 3)
                {
                    packageSourceDictionary1, // Pre-existing and unchanged.
                    // No source mapping is sent by Unified Settings for nonExistantSourceName2.
                    packageSourceDictionary3, // Pre-existing and unchanged.
                    packageSourceDictionary4 // A newly added source mapping.
                };

            // Act
            ExternalSettingOperationResult result = await instance.SetValueAsync(
                PackageSourceMappingPage.MonikerPackageSourceMapping,
                sourceMappingDictionaryList,
                CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeOfType<ExternalSettingOperationResult.Success>();

            IReadOnlyList<PackageSourceMappingSourceItem> packageSourceMappingItems = instance._packageSourceMappingProvider.GetPackageSourceMappingItems();

            // If the machine-wide config contains package source mappings, this count could be greater than 4.
            packageSourceMappingItems.Should().HaveCountGreaterThanOrEqualTo(4);

            packageSourceMappingItems.Should().Contain(unitTestingSourceMapping1);
            packageSourceMappingItems.Should().Contain(nonExistantSourceInSourceMapping2);
            packageSourceMappingItems.Should().Contain(unitTestingSourceMapping3);

            var foundResult = packageSourceMappingItems.Should().Contain(item => item.Key.Equals(addNewPackageSourceName4), because: "The package source was added in a new source mapping.");
            PackageSourceMappingSourceItem foundNewPackageSourceMapping = foundResult.Subject;
            foundNewPackageSourceMapping.Patterns.Should().ContainSingle(packagePatternItem => packagePatternItem.Pattern == packageIdPattern, because: "One pattern was added to the new source mapping.");
        }
    }
}
