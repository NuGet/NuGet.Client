// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;

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

            foreach (KeyValuePair<string, IReadOnlyList<string>> namespacePerSource in configuration.Namespaces)
            {
                foreach (string namespaceId in namespacePerSource.Value)
                {
                    PackageSource packageSource = configuration.GetPackageSource(namespacePerSource.Key);
                    Add(packageSource.Source, namespaceId);
                }
            }
        }

        private void Add(string packageSourceKey, string namespaceId)
        {
            SearchNode currentNode = _root;

            if (string.IsNullOrWhiteSpace(namespaceId))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Empty_Or_WhiteSpaceOnly, nameof(namespaceId));
            }

            // To prevent from unwanted behaviour.
            if (namespaceId.Length > PackageNamespacesConfiguration.PackageIdMaxLength)
            {
                throw new ArgumentOutOfRangeException(nameof(namespaceId));
            }

            packageSourceKey = packageSourceKey.Trim();
            namespaceId = namespaceId.ToLower(CultureInfo.CurrentCulture).Trim();

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

            if (currentNode.PackageSources == null)
            {
                currentNode.PackageSources = new List<string>();
            }

            currentNode.PackageSources.Add(packageSourceKey);
        }

        /// <summary>
        /// Get package source names with matching prefix "term" from package namespaces section.
        /// </summary>
        /// <param name="term">Search term. Cannot be null, empty, or whitespace only. </param>
        /// <returns>Package source names with matching prefix "term" from package namespaces.</returns>
        /// <exception cref="ArgumentException"> if <paramref name="term"/> is null, empty, or whitespace only.</exception>
        public IReadOnlyList<string> GetConfiguredPackageSources(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Empty_Or_WhiteSpaceOnly, nameof(term));
            }

            term = term.ToLower(CultureInfo.CurrentCulture).Trim();
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

            if (i == term.Length && currentNode.PackageSources != null)
            {
                return currentNode.PackageSources;
            }

            return longestMatchingPrefixNode?.PackageSources;
        }
    }
}
