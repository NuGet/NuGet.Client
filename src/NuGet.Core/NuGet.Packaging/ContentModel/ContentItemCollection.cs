// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.ContentModel.Infrastructure;
using NuGet.Shared;

namespace NuGet.ContentModel
{
    public class ContentItemCollection
    {
        private List<Asset> _assets;

        /// <summary>
        /// True if lib/contract exists
        /// </summary>
        public bool HasContract { get; private set; }

        public void Load(IEnumerable<string> paths)
        {
            // Read already loaded assets
            _assets = new List<Asset>();

            foreach (var path in paths)
            {
                // Skip files in the root of the directory
                if (IsValidAsset(path))
                {
                    _assets.Add(new Asset()
                    {
                        Path = path
                    });

                    if (path.StartsWith("lib/contract", StringComparison.Ordinal))
                    {
                        HasContract = true;

                        _assets.Add(new Asset
                        {
                            Path = "ref/any" + path.Substring("lib/contract".Length)
                        });
                    }
                }
            }
        }

        public IEnumerable<ContentItem> FindItems(PatternSet definition)
        {
            return FindItemsImplementation(definition, _assets);
        }

        public IEnumerable<ContentItemGroup> FindItemGroups(PatternSet definition)
        {
            if (_assets.Count > 0)
            {
                var groupPatterns = definition.GroupExpressions;

                List<Tuple<ContentItem, Asset>> groupAssets = null;
                foreach (var asset in _assets)
                {
                    foreach (var groupPattern in groupPatterns)
                    {
                        var item = groupPattern.Match(asset.Path, definition.PropertyDefinitions);
                        if (item != null)
                        {
                            if (groupAssets == null)
                            {
                                groupAssets = new List<Tuple<ContentItem, Asset>>(1);
                            }

                            groupAssets.Add(Tuple.Create(item, asset));
                        }
                    }
                }

                if (groupAssets?.Count > 0)
                {
                    foreach (var grouping in groupAssets.GroupBy(key => key.Item1, GroupComparer.DefaultComparer))
                    {
                        var group = new ContentItemGroup();

                        foreach (var property in grouping.Key.Properties)
                        {
                            group.Properties.Add(property.Key, property.Value);
                        }

                        foreach (var item in FindItemsImplementation(definition, grouping.Select(match => match.Item2)))
                        {
                            group.Items.Add(item);
                        }

                        yield return group;
                    }
                }
            }
        }

        public bool HasItemGroup(SelectionCriteria criteria, params PatternSet[] definitions)
        {
            return FindBestItemGroup(criteria, definitions) != null;
        }

        public ContentItemGroup FindBestItemGroup(SelectionCriteria criteria, params PatternSet[] definitions)
        {
            foreach (var definition in definitions)
            {
                var itemGroups = FindItemGroups(definition).ToList();
                foreach (var criteriaEntry in criteria.Entries)
                {
                    ContentItemGroup bestGroup = null;
                    var bestAmbiguity = false;

                    foreach (var itemGroup in itemGroups)
                    {
                        var groupIsValid = true;
                        foreach (var criteriaProperty in criteriaEntry.Properties)
                        {
                            if (criteriaProperty.Value == null)
                            {
                                if (itemGroup.Properties.ContainsKey(criteriaProperty.Key))
                                {
                                    groupIsValid = false;
                                    break;
                                }
                            }
                            else
                            {
                                object itemProperty;
                                if (!itemGroup.Properties.TryGetValue(criteriaProperty.Key, out itemProperty))
                                {
                                    groupIsValid = false;
                                    break;
                                }
                                ContentPropertyDefinition propertyDefinition;
                                if (!definition.PropertyDefinitions.TryGetValue(criteriaProperty.Key, out propertyDefinition))
                                {
                                    groupIsValid = false;
                                    break;
                                }
                                if (!propertyDefinition.IsCriteriaSatisfied(criteriaProperty.Value, itemProperty))
                                {
                                    groupIsValid = false;
                                    break;
                                }
                            }
                        }
                        if (groupIsValid)
                        {
                            if (bestGroup == null)
                            {
                                bestGroup = itemGroup;
                            }
                            else
                            {
                                var groupComparison = 0;
                                foreach (var criteriaProperty in criteriaEntry.Properties)
                                {
                                    if (criteriaProperty.Value == null)
                                    {
                                        continue;
                                    }
                                    else
                                    {
                                        var bestGroupValue = bestGroup.Properties[criteriaProperty.Key];
                                        var itemGroupValue = itemGroup.Properties[criteriaProperty.Key];
                                        var propertyDefinition = definition.PropertyDefinitions[criteriaProperty.Key];
                                        groupComparison = propertyDefinition.Compare(criteriaProperty.Value, bestGroupValue, itemGroupValue);
                                        if (groupComparison != 0)
                                        {
                                            break;
                                        }
                                    }
                                }
                                if (groupComparison > 0)
                                {
                                    bestGroup = itemGroup;
                                    bestAmbiguity = false;
                                }
                                else if (groupComparison == 0)
                                {
                                    bestAmbiguity = true;
                                }
                            }
                        }
                    }
                    if (bestGroup != null)
                    {
                        if (bestAmbiguity)
                        {
                            // var x = 5;
                        }
                        return bestGroup;
                    }
                }
            }
            return null;
        }

        private IEnumerable<ContentItem> FindItemsImplementation(PatternSet definition, IEnumerable<Asset> assets)
        {
            var pathPatterns = definition.PathExpressions;

            foreach (var asset in assets)
            {
                var path = asset.Path;

                foreach (var pathPattern in pathPatterns)
                {
                    var contentItem = pathPattern.Match(path, definition.PropertyDefinitions);
                    if (contentItem != null)
                    {
                        yield return contentItem;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// False if the path would not match any patterns.
        /// </summary>
        private static bool IsValidAsset(string path)
        {
            // Verify that the file is not in the root. All patterns are for sub directories.
            for (var i = 1; i < path.Length; i++)
            {
                if (path[i] == '/')
                {
                    return true;
                }
            }

            return false;
        }

        private class GroupComparer : IEqualityComparer<ContentItem>
        {
            public static readonly GroupComparer DefaultComparer = new GroupComparer();

            public int GetHashCode(ContentItem obj)
            {
                var hashCode = 0;
                foreach (var property in obj.Properties)
                {
                    if (property.Key.EndsWith("_raw", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    hashCode ^= property.Key.GetHashCode();
                    hashCode ^= property.Value.GetHashCode();
                }
                return hashCode;
            }

            public bool Equals(ContentItem x, ContentItem y)
            {
                if (ReferenceEquals(x, y))
                {
                    return true;
                }

                if (x.Properties.Count != y.Properties.Count)
                {
                    return false;
                }

                foreach (var xProperty in x.Properties)
                {
                    if (xProperty.Key.EndsWith("_raw", StringComparison.OrdinalIgnoreCase))
                    {
                        // We've started storing raw versions of each key, but
                        // we don't want that to affect results.
                        continue;
                    }
                    object yValue;
                    if (!y.Properties.TryGetValue(xProperty.Key, out yValue))
                    {
                        return false;
                    }
                    if (!Equals(xProperty.Value, yValue))
                    {
                        return false;
                    }
                }

                foreach (var yProperty in y.Properties)
                {
                    object xValue;
                    if (!x.Properties.TryGetValue(yProperty.Key, out xValue))
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}
