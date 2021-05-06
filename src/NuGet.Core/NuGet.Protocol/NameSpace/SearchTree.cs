// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Protocol
{
    public class SearchTree : INameSpaceLookup
    {
        private readonly SearchNode _root;
        private const int PackageIdMaxLength = 100;

        public SearchTree(IReadOnlyList<PackageSourceSection> packageSourceSections)
        {
            if (packageSourceSections == null)
            {
                throw new ArgumentNullException(nameof(packageSourceSections));
            }

            _root = new SearchNode(null);

            foreach (PackageSourceSection packageSourceSection in packageSourceSections)
            {
#pragma warning disable CA1308 // Normalize strings to uppercase
                var packageSourceKey = packageSourceSection.GetPackageSourceKey().ToLowerInvariant();
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

            if (namespaceId.Length > PackageIdMaxLength)
            {
                // make it resource string.
                // For the efficiency and prevent from unwanted behaviour.
                throw new ArgumentOutOfRangeException($"NamespaceId '{namespaceId}' in packageNamespaces section of nuget.config is too long. Please consider to reduce it.");
            }

#pragma warning disable CA1308 // Normalize strings to uppercase
            namespaceId = namespaceId.ToLowerInvariant();
#pragma warning restore CA1308 // Normalize strings to uppercase

            for (int i = 0; i < namespaceId.Length; i++)
            {
                char c = namespaceId[i];

                if (c == '.')
                {
                    c = (char)(26 + 'a');
                }
                else if (c == '*')
                {
                    // break here since seeing * means end of expression.
                    currentNode.IsGlobbing = true;
                    break;
                }

                if (!currentNode.Children.ContainsKey(c))
                {
                    currentNode.Children[c] = new SearchNode(currentNode);
                }

                currentNode.IsLeaf = false;
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

        public (bool Prefixmath, bool ValueMatch, bool IsLeaf, HashSet<string> PackageSources) Find(string term)
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
                    return currentNode.IsGlobbing ? (true, false, currentNode.IsLeaf, currentNode.PackageSources) : (false, false, currentNode.IsLeaf, currentNode.PackageSources);
                }

                currentNode = currentNode.Children[c];
            }

            if (i == term.Length && !currentNode.IsValueNode) // full 'term' already matched, but still not at value Node. That means we need to go backtrace try to find better matching sources.
            {
                while(currentNode != null && !currentNode.IsValueNode)
                {
                    currentNode = currentNode.Parent;
                }
            }

            return currentNode == null ? (false, false, false, null)
                                        :(true, currentNode.IsValueNode, currentNode.IsLeaf, currentNode.PackageSources);
        }
    }
}
