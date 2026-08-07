// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Configuration
{
    public sealed class MinPublishAgeExceptions
    {
        private readonly SearchTree _searchTree;
        private readonly IReadOnlyDictionary<string, MinPublishAgeExceptionItem> _itemsByPattern;

        public bool IsEnabled { get; }

        public MinPublishAgeExceptions(IReadOnlyList<MinPublishAgeExceptionItem> items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            IsEnabled = items.Count > 0;
            var itemsByPattern = new Dictionary<string, MinPublishAgeExceptionItem>(items.Count, StringComparer.OrdinalIgnoreCase);
            var patterns = new string[items.Count];
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null)
                {
                    throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(items));
                }

                string pattern = item.Pattern;
                itemsByPattern[pattern] = item;
                patterns[i] = pattern;
            }

            var mapping = new PackageSourceMapping(new Dictionary<string, IReadOnlyList<string>>
            {
                ["minPublishAgeExceptions"] = patterns
            });

            _itemsByPattern = itemsByPattern;
            _searchTree = new SearchTree(mapping);
        }

        public MinPublishAgeExceptionItem? FindException(string packageId)
        {
            string? matchingPattern = _searchTree.SearchForPattern(packageId);
            return matchingPattern != null && _itemsByPattern.TryGetValue(matchingPattern, out var item)
                ? item
                : null;
        }
    }
}
