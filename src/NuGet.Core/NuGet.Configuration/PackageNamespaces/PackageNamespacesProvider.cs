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

        public IReadOnlyList<PackageSourceNamespacesItem> GetPackageSourceNamespaces()
        {
            var packageNamespacesSection = _settings.GetSection(ConfigurationConstants.PackageNamespaces);
            if (packageNamespacesSection == null)
            {
                return Enumerable.Empty<PackageSourceNamespacesItem>().ToList();
            }

            return packageNamespacesSection.Items.OfType<PackageSourceNamespacesItem>().ToList();
        }

        public void Remove(IReadOnlyList<PackageSourceNamespacesItem> packageSourceNamespaces)
        {
            if (packageSourceNamespaces == null || !packageSourceNamespaces.Any())
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(packageSourceNamespaces));
            }

            foreach (var packageSourceNamespace in packageSourceNamespaces)
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

        public void AddOrUpdatePackageSourceNamespace(PackageSourceNamespacesItem packageSourceNamespace)
        {
            if (packageSourceNamespace == null)
            {
                throw new ArgumentNullException(nameof(packageSourceNamespace));
            }

            _settings.AddOrUpdate(ConfigurationConstants.PackageNamespaces, packageSourceNamespace);

            _settings.SaveToDisk();
        }
    }
}
