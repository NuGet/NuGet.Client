// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Configuration
{
    internal class PackageNamespacesProvider
    {
        private readonly ISettings _settings;

        public PackageNamespacesProvider(ISettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public IReadOnlyList<PackageNamespacesSourceItem> GetPackageSourceNamespaces()
        {
            SettingSection packageNamespacesSection = _settings.GetSection(ConfigurationConstants.PackageNamespaces);
            if (packageNamespacesSection == null)
            {
                return Enumerable.Empty<PackageNamespacesSourceItem>().ToList();
            }

            return packageNamespacesSection.Items.OfType<PackageNamespacesSourceItem>().ToList();
        }

        public void Remove(IReadOnlyList<PackageNamespacesSourceItem> packageNamespacesSource)
        {
            if (packageNamespacesSource == null || packageNamespacesSource.Count == 0)
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(packageNamespacesSource));
            }

            foreach (PackageNamespacesSourceItem packageSourceNamespace in packageNamespacesSource)
            {
                try
                {
                    _settings.Remove(ConfigurationConstants.PackageNamespaces, packageSourceNamespace);
                }
                // An error means the item doesn't exist or is in a machine wide config, therefore just ignore it
                catch { }
            }

            _settings.SaveToDisk();
        }

        public void AddOrUpdatePackageSourceNamespace(PackageNamespacesSourceItem packageNamespacesSource)
        {
            if (packageNamespacesSource == null)
            {
                throw new ArgumentNullException(nameof(packageNamespacesSource));
            }

            _settings.AddOrUpdate(ConfigurationConstants.PackageNamespaces, packageNamespacesSource);

            _settings.SaveToDisk();
        }

        internal IReadOnlyList<PackageSource> GetPackageSources()
        {
            var packageSourceProvider = new PackageSourceProvider(_settings);
            return packageSourceProvider.LoadPackageSources().ToList();
        }
    }
}
