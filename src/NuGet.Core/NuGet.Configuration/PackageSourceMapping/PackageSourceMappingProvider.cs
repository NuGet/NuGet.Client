// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Configuration
{
    internal class PackageSourceMappingProvider
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
    }
}
