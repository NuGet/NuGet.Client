// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Moq;
using NuGet.Configuration;

namespace Test.Utility
{
    public class PackageNamespacesConfigurationUtility
    {
        public static PackageNamespacesConfiguration GetPackageNamespacesConfiguration(string packageNamespaces)
        {
            string[] sections = packageNamespaces.Split('|');
            var sourceKeys = new HashSet<string>();
            var namespaces = new Dictionary<string, IReadOnlyList<string>>();

            var namespacesList = new List<PackageNamespacesSourceItem>();

            foreach (string section in sections)
            {
                string[] parts = section.Split(',');
                string sourceKey = parts[0];

                if (string.IsNullOrWhiteSpace(sourceKey))
                {
                    continue;
                }

                sourceKeys.Add(sourceKey);

                var namespaceItems = new List<NamespaceItem>();
                for (int i = 1; i < parts.Length; i++)
                {
                    namespaceItems.Add(new NamespaceItem(parts[i]));
                }

                namespacesList.Add(new PackageNamespacesSourceItem(sourceKey, namespaceItems));
            }

            var packageSourcesVirtualSection = new VirtualSettingSection(ConfigurationConstants.PackageSources,
                sourceKeys.Select(ns => new SourceItem(ns, ns.Trim() + ".com")).ToArray()
                );

            var packageNamespacesVirtualSection = new VirtualSettingSection(ConfigurationConstants.PackageNamespaces,
                namespacesList.ToArray()
                );

            var settings = new Mock<ISettings>(MockBehavior.Loose);

            settings.Setup(s => s.GetSection("packageSources"))
                .Returns(packageSourcesVirtualSection)
                .Verifiable();
            settings.Setup(s => s.GetConfigFilePaths())
                .Returns(new List<string>());
            settings.Setup(s => s.GetSection(ConfigurationConstants.DisabledPackageSources))
                .Returns(new VirtualSettingSection(ConfigurationConstants.DisabledPackageSources))
                .Verifiable();
            settings.Setup(s => s.GetSection(ConfigurationConstants.PackageNamespaces))
                    .Returns(packageNamespacesVirtualSection);

            return PackageNamespacesConfiguration.GetPackageNamespacesConfiguration(settings.Object);
        }
    }
}
