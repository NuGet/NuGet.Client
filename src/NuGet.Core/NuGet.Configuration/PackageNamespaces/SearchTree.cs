// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Configuration
{
    internal class SearchTree
    {
        private readonly SearchNode _root;
        private const int PackageIdMaxLength = 100;

        internal SearchTree(PackageNamespacesConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            _root = new SearchNode(null);

            foreach (string packageSourceKey in configuration.Namespaces.Keys)
            {
                foreach (string nugetNamespace in configuration.Namespaces[packageSourceKey])
                {
                    Add(packageSourceKey, nugetNamespace);
                }
            }
        }

        private void Add(string packageSourceKey, string namespaceId)
        {
            SearchNode currentNode = _root;

            if (namespaceId == null)
            {
                throw new ArgumentNullException(nameof(namespaceId));
            }

            // To prevent from unwanted behaviour.
            if (namespaceId.Length > PackageIdMaxLength)
            {
                throw new ArgumentOutOfRangeException(nameof(namespaceId));
            }

#pragma warning disable CA1308 // Normalize strings to uppercase
            packageSourceKey = packageSourceKey.ToLowerInvariant().Trim();
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

        public HashSet<string> PrefixMatch(string term)
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
                    return currentNode.IsGlobbing ? currentNode.PackageSources : null;
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

            return currentNode == null ? null
                                        : currentNode.PackageSources;
        }
    }
}
