// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Configuration
{
    internal class SearchTree
    {
        private readonly SearchNode _root;

        internal SearchTree(PackageNamespacesConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            _root = new SearchNode();

            foreach (var namespacePerSource in configuration.Namespaces)
            {
                foreach (var namespaceId in namespacePerSource.Value)
                {
                    Add(namespacePerSource.Key, namespaceId);
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
            if (namespaceId.Length > PackageNamespacesConfiguration.PackageIdMaxLength)
            {
                throw new ArgumentOutOfRangeException(nameof(namespaceId));
            }

            packageSourceKey = packageSourceKey.ToLowerInvariant().Trim();
            namespaceId = namespaceId.ToLowerInvariant().Trim();

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
                    currentNode.Children[c] = new SearchNode();
                }

                currentNode = currentNode.Children[c];
            }

            if (string.IsNullOrEmpty(currentNode.NamespaceId))
            {
                currentNode.NamespaceId = namespaceId;
            }

            if (currentNode.PackageSources == null)
            {
                currentNode.PackageSources = new List<string>();
            }

            currentNode.PackageSources.Add(packageSourceKey.ToLowerInvariant());
        }

        /// <summary>
        /// Get package source names with matching prefix "term" from package namespaces section.
        /// </summary>
        /// <param name="term">Search term. Never null. </param>
        /// <returns>Package source names with matching prefix "term" from package namespaces.</returns>
        /// <exception cref="ArgumentNullException"> if <paramref name="term"/> is null or empty.</exception>
        public List<string> GetConfiguredPackageSources(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                throw new ArgumentNullException(nameof(term));
            }

            term = term.ToLowerInvariant().Trim();
            SearchNode currentNode = _root;
            SearchNode longestMatchingPrefixNode = null;

            if (currentNode.IsGlobbing)
            {
                longestMatchingPrefixNode = currentNode;
            }

            int i = 0;

            for (; i < term.Length; i++)
            {
                char c = term[i];

                if (!currentNode.Children.ContainsKey(c))
                {
                    break;
                }

                currentNode = currentNode.Children[c];

                if (currentNode.IsGlobbing)
                {
                    longestMatchingPrefixNode = currentNode;
                }
            }

            // Full term match.
            if (i == term.Length && currentNode.IsValueNode)
            {
                return currentNode.PackageSources;
            }

            return longestMatchingPrefixNode == null ? null
                                        : longestMatchingPrefixNode.PackageSources;
        }
    }
}
