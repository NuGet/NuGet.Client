// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Configuration;

namespace Test.Utility
{
    public class PackageNamespacesConfigurationUtility
    {
        public static PackageNamespacesConfiguration GetPackageNamespacesConfiguration(string packageNamespaces)
        {
            string[] sections = packageNamespaces.Split('|');
            var namespaces = new Dictionary<string, IReadOnlyList<string>>();

            foreach (string section in sections)
            {
                string[] parts = section.Split(',');
                string sourceKey = parts[0];

                if (string.IsNullOrWhiteSpace(sourceKey))
                {
                    continue;
                }

                var namespaceList = new List<string>();
                for (int i = 1; i < parts.Length; i++)
                {
                    namespaceList.Add(parts[i]);
                }

                namespaces[sourceKey] = namespaceList;
            }
            ;
            return new PackageNamespacesConfiguration(namespaces);
        }
    }
}
