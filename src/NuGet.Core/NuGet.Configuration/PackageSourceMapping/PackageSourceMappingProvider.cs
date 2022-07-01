// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace NuGet.Configuration
{
    public class PackageSourceMappingProvider
    {
        private readonly ISettings _settings;

        public PackageSourceMappingProvider(ISettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public IReadOnlyList<PackageSourceMappingSourceItem> GetPackageSourceMappingItems()
        {
            SettingSection packageSourceMappingSection = _settings.GetSection(ConfigurationConstants.PackageSourceMapping);
            if (packageSourceMappingSection == null)
            {
                return Enumerable.Empty<PackageSourceMappingSourceItem>().ToList();
            }

            return packageSourceMappingSection.Items.OfType<PackageSourceMappingSourceItem>().ToList();
        }

        public void Remove(IReadOnlyList<PackageSourceMappingSourceItem> packageSourceMappingSourceItems)
        {
            if (packageSourceMappingSourceItems == null || packageSourceMappingSourceItems.Count == 0)
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(packageSourceMappingSourceItems));
            }

            foreach (PackageSourceMappingSourceItem packageSourceMappingSourceItem in packageSourceMappingSourceItems)
            {
                try
                {
                    _settings.Remove(ConfigurationConstants.PackageSourceMapping, packageSourceMappingSourceItem);
                }
                // An error means the item doesn't exist or is in a machine wide config, therefore just ignore it
                catch { }
            }

            _settings.SaveToDisk();
        }

        public void AddOrUpdatePackageSourceMappingSourceItem(PackageSourceMappingSourceItem packageSourceMappingSourceItem)
        {
            if (packageSourceMappingSourceItem == null)
            {
                throw new ArgumentNullException(nameof(packageSourceMappingSourceItem));
            }

            _settings.AddOrUpdate(ConfigurationConstants.PackageSourceMapping, packageSourceMappingSourceItem);

            _settings.SaveToDisk();
        }

        public void SavePackageSourceMappings(IReadOnlyList<PackageSourceMappingSourceItem> mappings)
        {
            if (mappings == null)
            {
                throw new ArgumentNullException(nameof(mappings));
            }
#pragma warning disable CS0618 // Type or member is obsolete
            SavePackageSourcesMappings(mappings, PackageSourceUpdateOptions.Default);
#pragma warning restore CS0618 // Type or member is obsolete
        }

        [Obsolete("https://github.com/NuGet/Home/issues/10098")]
        public void SavePackageSourcesMappings(IReadOnlyList<PackageSourceMappingSourceItem> packageSourceMappingsSourceItems, PackageSourceUpdateOptions sourceUpdateSettings)
        {
            if (packageSourceMappingsSourceItems == null)
            {
                throw new ArgumentNullException(nameof(packageSourceMappingsSourceItems));
            }


            var existingSettingsLookup = GetPackageSourceMappingItems();

            foreach (var sourceMappingItem in packageSourceMappingsSourceItems)
            {
                if (existingSettingsLookup != null)
                {
                    AddOrUpdatePackageSourceMappingSourceItem(sourceMappingItem);
                }
            }

            //Remove all old mappings not in new mappings
            if (existingSettingsLookup != null)
            {
                ObservableCollection<PackageSourceMappingSourceItem> removeMappings = new ObservableCollection<PackageSourceMappingSourceItem>();
                foreach (var sourceItem in existingSettingsLookup)
                {
                    if (!packageSourceMappingsSourceItems.Contains(sourceItem))
                    {
                        removeMappings.Add(sourceItem);
                    }
                }
                if (removeMappings != null && removeMappings.Count > 0)
                {
                    Remove(removeMappings);
                }
            }
        }
    }
}
