// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.ContentModel.Infrastructure;

namespace NuGet.ContentModel
{
    public class ContentItemCollection
    {
        private static readonly GroupComparer _groupComparer = new GroupComparer();

        private List<Asset> _assets;

        public void Load(IEnumerable<string> paths)
        {
            var list = new List<Asset>();
            foreach (var path in paths)
            {
                list.Add(new Asset { Path = path });

                if (path.StartsWith("lib/contract"))
                {
                    list.Add(new Asset
                    {
                        Path = "ref/any" + path.Substring("lib/contract".Length),
                        Link = path
                    });
                }
            }

            _assets = list;
        }

        public void Load(string packageDirectory)
        {
            var packages = AssetManager.GetPackageAssets(packageDirectory);

            var list = new List<Asset>();
            foreach (var asset in packages)
            {
                list.Add(asset);

                if (asset.Path.StartsWith("lib/contract"))
                {
                    list.Add(new Asset
                    {
                        Path = "ref/any" + asset.Path.Substring("lib/contract".Length),
                        Link = asset.Link ?? asset.Path
                    });
                }
            }

            _assets = list;
        }

        public IEnumerable<ContentItem> FindItems(PatternSet definition)
        {
            return FindItemsImplementation(definition, _assets);
        }

        public IEnumerable<ContentItemGroup> FindItemGroups(PatternSet definition)
        {
            return FindItemGroupsImplementation(definition);
        }

        public bool HasItemGroup(SelectionCriteria criteria, params PatternSet[] definitions)
        {
            return FindBestItemGroup(criteria, definitions) != null;
        }

        public ContentItemGroup FindBestItemGroup(SelectionCriteria criteria, params PatternSet[] definitions)
        {
            foreach (var definition in definitions)
            {
                var itemGroups = FindItemGroupsImplementation(definition);
                var criteriaEntries = criteria.Entries as List<SelectionCriteriaEntry> ?? criteria.Entries.ToList();
                foreach (var criteriaEntry in criteriaEntries)
                {
                    ContentItemGroup bestGroup = null;
                    var bestAmbiguity = false;

                    foreach (var itemGroup in itemGroups)
                    {
                        var groupIsValid = true;
                        var criteriaEntryProperties = criteriaEntry.Properties as Dictionary<string, object> ?? criteriaEntry.Properties.ToDictionary(s => s.Key, s => s.Value);
                        foreach (var criteriaProperty in criteriaEntryProperties)
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
                                foreach (var criteriaProperty in criteriaEntryProperties)
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

        private List<ContentItem> FindItemsImplementation(PatternSet definition, List<Asset> assets)
        {
            var pathPatterns = definition.PathPatterns
                .Select(pattern => new PatternExpression(pattern))
                .ToList();

            var list = new List<ContentItem>();

            foreach (var asset in assets)
            {
                foreach (var pathPattern in pathPatterns)
                {
                    var contentItem = pathPattern.Match(asset.Path, definition.PropertyDefinitions);
                    if (contentItem != null)
                    {
                        list.Add(contentItem);
                        break;
                    }
                }
            }

            return list;
        }

        private List<ContentItemGroup> FindItemGroupsImplementation(PatternSet definition)
        {
            var list = new List<ContentItemGroup>();
            var groupPatterns = definition.GroupPatterns
                .Select(pattern => new PatternExpression(pattern))
                .ToList();

            var groupAssets = new List<Tuple<ContentItem, Asset>>();
            foreach (var asset in _assets)
            {
                foreach (var groupPattern in groupPatterns)
                {
                    var item = groupPattern.Match(asset.Path, definition.PropertyDefinitions);
                    if (item != null)
                    {
                        groupAssets.Add(Tuple.Create(item, asset));
                    }
                }
            }

            foreach (var grouping in groupAssets.GroupBy(key => key.Item1, _groupComparer))
            {
                var group = new ContentItemGroup();

                foreach (var property in (Dictionary<string, object>)grouping.Key.Properties)
                {
                    group.Properties.Add(property.Key, property.Value);
                }

                foreach (var item in FindItemsImplementation(definition, grouping.Select(match => match.Item2).ToList()))
                {
                    group.Items.Add(item);
                }

                list.Add(group);
            }

            return list;
        }

        private class GroupComparer : IEqualityComparer<ContentItem>
        {
            public int GetHashCode(ContentItem obj)
            {
                var hashCode = 0;
                foreach (var property in (Dictionary<string, object>)obj.Properties)
                {
                    hashCode ^= property.Key.GetHashCode();
                    hashCode ^= property.Value.GetHashCode();
                }
                return hashCode;
            }

            public bool Equals(ContentItem x, ContentItem y)
            {
                foreach (var xProperty in (Dictionary<string, object>)x.Properties)
                {
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

                foreach (var yProperty in (Dictionary<string, object>)y.Properties)
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
