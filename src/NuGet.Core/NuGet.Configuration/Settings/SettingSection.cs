// Copyright(c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace NuGet.Configuration
{
    public abstract class SettingSection : SettingsGroup<SettingItem>, IEquatable<SettingSection>
    {
        public IReadOnlyCollection<SettingItem> Items => ChildrenSet.Select(c => c.Value).ToList();

        public T GetFirstItemWithAttribute<T>(string attributeName, string expectedAttributeValue) where T : SettingItem
        {
            return Items.OfType<T>().FirstOrDefault(c =>
                c.Attributes.TryGetValue(attributeName, out var attributeValue) &&
                string.Equals(attributeValue, expectedAttributeValue, StringComparison.OrdinalIgnoreCase));
        }

        protected SettingSection(string name, IReadOnlyDictionary<string, string> attributes, IEnumerable<SettingItem> children)
            : base(attributes, children)
        {
            Name = XmlConvert.EncodeLocalName(name) ?? throw new ArgumentNullException(nameof(name));
        }

        internal SettingSection(XElement element, SettingsFile origin)
            : base(element, origin)
        {
        }

        internal bool Update(SettingItem item)
        {
            if (item == null || (Origin != null && Origin.IsMachineWide))
            {
                return false;
            }

            if (ChildrenSet.ContainsKey(item))
            {
                var currentChild = ChildrenSet[item];

                if (currentChild.Origin.IsMachineWide)
                {
                    return false;
                }

                currentChild.Update(item);

                return true;
            }

            return false;
        }

        public bool Equals(SettingSection other)
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

        public bool DeepEquals(SettingSection other)
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
                Items.SequenceEqual(other.Items);
        }

        public override bool DeepEquals(SettingBase other) => DeepEquals(other as SettingSection);
        public override bool Equals(SettingBase other) => Equals(other as SettingSection);
        public override bool Equals(object other) => Equals(other as SettingSection);
        public override int GetHashCode() => Name.GetHashCode();
    }
}
