// Copyright(c) .NET Foundation.All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace NuGet.Configuration
{
    // This could also be an abstract generic class where T is a settingsItem if we wanted to constraint each section to an specific type of item
    public sealed class SettingsSection : SettingsCollection<SettingsItem>, IEquatable<SettingsSection>
    {
        public SettingsSection(SettingsSection instance)
            : base(instance.Children)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            Name = instance.Name;
        }

        internal SettingsSection(XElement element, ISettingsFile origin)
            : base(element, origin)
        {
        }

        public SettingsSection(string name)
            : this(name, children:  new SettingsItem[] { })
        {
        }

        public SettingsSection(string name, params SettingsItem[] children)
            : base(new HashSet<SettingsItem>(children))
        {
            Name = XmlConvert.EncodeLocalName(name) ?? throw new ArgumentNullException(nameof(name));
        }

        public SettingsSection Merge(SettingsSection other)
        {
            if (!Equals(other))
            {
                throw new ArgumentException("Cannot merge two different sections", nameof(other));
            }

            foreach (var child in other.Children)
            {
                if (child != null)
                {
                    if (child is ClearItem)
                    {
                        if (CanBeCleared)
                        {
                            ChildrenSet.Clear();
                        }

                        ChildrenSet.Add(child, child);

                        continue;
                    }

                    if (ChildrenSet.ContainsKey(child))
                    {
                        child.MergedWith = ChildrenSet[child];
                        ChildrenSet[child] = child;
                    }
                    else
                    {
                        ChildrenSet.Add(child, child);
                    }
                }
            }

            return this;
        }

        public T GetFirstItemWithAttribute<T>(string attributeName, string expectedAttributeValue) where T : SettingsItem
        {
            return Children.FirstOrDefault(c =>
                c.TryGetAttributeValue(attributeName, out var attributeValue) &&
                string.Equals(attributeValue, expectedAttributeValue) &&
                c is T) as T;
        }

        public bool TryUpdateChildItem(SettingsItem item, bool isBatchOperation = false)
        {
            if (item == null || (Origin != null && Origin.IsMachineWide))
            {
                return false;
            }

            if (ChildrenSet.ContainsKey(item))
            {
                var currentChild = Children.FirstOrDefault(c => c.Equals(item));
                if (currentChild.Origin.IsMachineWide)
                {
                    return false;
                }

                if (currentChild.Update(item))
                {
                    if (!isBatchOperation)
                    {
                        currentChild.Origin.Save();
                    }

                    return true;
                }
            }

            return false;
        }

        public bool HasClearChild()
        {
            return ChildrenSet.TryGetValue(new ClearItem(), out var _);
        }

        public bool Equals(SettingsSection other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return string.Equals(Name, other.Name, StringComparison.Ordinal);
        }

        public bool DeepEquals(SettingsSection other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return string.Equals(Name, other.Name, StringComparison.Ordinal) &&
                Children.SequenceEqual(other.Children);
        }

        public override bool DeepEquals(SettingsNode other) => DeepEquals(other as SettingsSection);
        public override bool Equals(SettingsNode other) => Equals(other as SettingsSection);
        public override bool Equals(object other) => Equals(other as SettingsSection);
        public override int GetHashCode() => Name.GetHashCode();
    }
}
