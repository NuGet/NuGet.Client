// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NuGet.Common;

namespace NuGet.Configuration
{
    public class SearchTree
    {
        private readonly SearchNode _root;
        private const int PackageIdMaxLength = 100;

        internal SearchTree(IReadOnlyList<PackageSourceSection> packageSourceSections)
        {
            if (packageSourceSections == null)
            {
                throw new ArgumentNullException(nameof(packageSourceSections));
            }

            _root = new SearchNode(null);

            foreach (PackageSourceSection packageSourceSection in packageSourceSections)
            {
#pragma warning disable CA1308 // Normalize strings to uppercase
                var packageSourceKey = packageSourceSection.GetPackageSourceKey().ToLowerInvariant().Trim();
#pragma warning restore CA1308 // Normalize strings to uppercase

                foreach (string nameSpaceId in packageSourceSection.GetNameSpaceIds())
                {
                    Add(nameSpaceId, packageSourceKey);
                }
            }
        }

        private void Add(string namespaceId, string packageSourceKey)
        {
            SearchNode currentNode = _root;

            if (namespaceId == null)
            {
                throw new ArgumentNullException(nameof(namespaceId));
            }

            // For prevent from unwanted behaviour.
            if (namespaceId.Length > PackageIdMaxLength)
            {
                throw new ArgumentOutOfRangeException(nameof(namespaceId));
            }

#pragma warning disable CA1308 // Normalize strings to uppercase
            namespaceId = namespaceId.ToLowerInvariant().Trim();
#pragma warning restore CA1308 // Normalize strings to uppercase

            for (int i = 0; i < namespaceId.Length; i++)
            {
                char c = namespaceId[i];

                if (c == '*')
                {
                    // break here since seeing * means end of expression.
                    currentNode.IsGlobbing = true;
                    break;
                }

                if (!currentNode.Children.ContainsKey(c))
                {
                    currentNode.Children[c] = new SearchNode(currentNode);
                }

                currentNode = currentNode.Children[c];
            }

            if (string.IsNullOrEmpty(currentNode.NamespaceId))
            {
                currentNode.NamespaceId = namespaceId;
            }

            if (currentNode.PackageSources == null)
            {
                currentNode.PackageSources = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            }

            currentNode.PackageSources.Add(packageSourceKey);
        }

        public ConfigNameSpaceLookup Find(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                throw new ArgumentNullException(nameof(term));
            }

#pragma warning disable CA1308 // Normalize strings to uppercase
            term = term.ToLowerInvariant().Trim();
#pragma warning restore CA1308 // Normalize strings to uppercase
            SearchNode currentNode = _root;
            int i = 0;

            for (; i < term.Length; i++)
            {
                char c = term[i];

                if (c == '*')
                {
                    // break here since seeing * means end of expression.
                    break;
                }

                if (!currentNode.Children.ContainsKey(c))
                {
                    return currentNode.IsGlobbing ? new ConfigNameSpaceLookup(true, currentNode.PackageSources) : new ConfigNameSpaceLookup(false, currentNode.PackageSources);
                }

                currentNode = currentNode.Children[c];
            }

            if (i == term.Length && !currentNode.IsValueNode) // full 'term' already matched, but still not at value Node. That means we need to go backtrace try to find better matching sources.
            {
                while (currentNode != null && !currentNode.IsValueNode)
                {
                    currentNode = currentNode.Parent;
                }
            }

            return currentNode == null ? new ConfigNameSpaceLookup(false, null)
                                        : new ConfigNameSpaceLookup(true, currentNode.PackageSources);
        }

        public static SearchTree GetSearchTree(ISettings settings, ILogger logger)
        {
            SearchTree nameSpaceLookup = null;

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            PackageNamespacesConfiguration configuration = PackageNamespacesConfiguration.GetPackageNamespacesConfiguration(settings);
            var packageSourceSections = new List<PackageSourceSection>();

            foreach (var packageSourceKey in configuration.Namespaces.Keys)
            {
                string[] nugetNamespaces = configuration.Namespaces[packageSourceKey].ToArray();
                packageSourceSections.Add(new PackageSourceSection(nugetNamespaces, packageSourceKey));
            }

            if (packageSourceSections.Any())
            {
                nameSpaceLookup = new SearchTree(packageSourceSections);
                logger?.LogDebug(string.Format(CultureInfo.CurrentCulture, Resources.PackageNamespaceFound));
            }

            return nameSpaceLookup;
        }
    }
}
