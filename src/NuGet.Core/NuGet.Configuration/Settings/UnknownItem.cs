// Copyright(c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using NuGet.Shared;

namespace NuGet.Configuration
{
    public sealed class UnknownItem : SettingItem, ISettingsGroup, IEquatable<UnknownItem>
    {
        public override string Name { get; protected set; }

        public IReadOnlyList<SettingBase> Children => MutableChildren.Select(c => c.Value).ToList().AsReadOnly();

        public new IReadOnlyDictionary<string, string> Attributes => base.Attributes;

        internal override bool IsEmpty() => false;

        protected override bool CanHaveChildren => true;

        private Dictionary<SettingBase, SettingBase> MutableChildren { get; set; }

        internal UnknownItem(XElement element, SettingsFile origin)
            : base(element, origin)
        {
            Name = element.Name.LocalName;
            MutableChildren = new Dictionary<SettingBase, SettingBase>();

            var descendants = element.Nodes().Where(n => n is XText text && !string.IsNullOrWhiteSpace(text.Value) || n is XElement)
                .Select(d => SettingFactory.Parse(d, origin)).Distinct();

            foreach (var descendant in descendants)
            {
                descendant.Parent = this;

                MutableChildren.Add(descendant, descendant);
            }
        }

        public UnknownItem(string name, IReadOnlyDictionary<string, string> attributes, IEnumerable<SettingBase> children)
            : base(attributes)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            Name = name;
            MutableChildren = new Dictionary<SettingBase, SettingBase>();

            if (children != null)
            {
                foreach (var child in children)
                {
                    child.Parent = this;
                    MutableChildren.Add(child, child);
                }
            }
        }

        internal override SettingBase Clone() => new UnknownItem(Name, Attributes, Children.Select(c => c.Clone())) { Origin = Origin };

        internal bool Add(SettingBase setting)
        {
            if (setting == null)
            {
                throw new ArgumentNullException(nameof(setting));
            }

            if (Origin.IsMachineWide)
            {
                throw new InvalidOperationException(Resources.CannotUpdateMachineWide);
            }

            if (!MutableChildren.ContainsKey(setting) && !setting.IsEmpty())
            {
                MutableChildren.Add(setting, setting);

                setting.AddToOrigin(Origin);
                setting.Node = setting.AsXNode();

                XElementUtility.AddIndented(Node as XElement, setting.Node);
                setting.Parent = this;
                Origin.IsDirty = true;

                return true;
            }

            return false;
        }

        void ISettingsGroup.Remove(SettingElement setting)
        {
            Remove(setting);
        }

        internal void Remove(SettingBase setting)
        {
            if (setting == null)
            {
                throw new ArgumentNullException(nameof(setting));
            }

            if (Origin != null && Origin.IsMachineWide)
            {
                throw new InvalidOperationException(Resources.CannotUpdateMachineWide);
            }

            if (MutableChildren.TryGetValue(setting, out var currentSetting) && MutableChildren.Remove(currentSetting))
            {
                XElementUtility.RemoveIndented(currentSetting.Node);
                Origin.IsDirty = true;

                currentSetting.Origin = null;
                currentSetting.Node = null;
                currentSetting.Parent = null;

                if (Parent != null && IsEmpty())
                {
                    Parent.Remove(this);
                }
            }
        }

        internal override XNode AsXNode()
        {
            if (Node != null && Node is XElement)
            {
                return Node;
            }

            var element = new XElement(Name, Children.Select(c => c.AsXNode()));
            foreach (var attr in Attributes)
            {
                element.SetAttributeValue(attr.Key, attr.Value);
            }

            return element;
        }

        public bool Equals(UnknownItem other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);
        }

        public bool DeepEquals(UnknownItem other)
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
                other.Attributes.Count == Attributes.Count &&
                Attributes.OrderedEquals(other.Attributes, data => data.Key, StringComparer.OrdinalIgnoreCase) &&
                Children.SequenceEqual(other.Children);
        }

        public override bool Equals(SettingBase other) => Equals(other as UnknownItem);
        public override bool DeepEquals(SettingBase other) => DeepEquals(other as UnknownItem);
        public override bool Equals(object other) => Equals(other as UnknownItem);
        public override int GetHashCode() => Name.GetHashCode();

        internal override void Update(SettingItem other)
        {
            base.Update(other);

            var unknown = other as UnknownItem;

            var otherChildren = new Dictionary<SettingBase, SettingBase>(unknown.MutableChildren);
            foreach (var child in Children)
            {
                if (otherChildren.TryGetValue(child, out var otherChild))
                {
                    otherChildren.Remove(child);
                }

                if (otherChild == null)
                {
                    Remove(child);
                }
                else if (!child.DeepEquals(otherChild) && child is SettingItem item)
                {
                    item.Update(otherChild as SettingItem);
                }
            }

            foreach (var newChild in otherChildren)
            {
                Add(newChild.Value);
            }
        }

        internal void Merge(UnknownItem item)
        {
            foreach (var attribute in item.Attributes)
            {
                AddOrUpdateAttribute(attribute.Key, attribute.Value);
            }

            foreach (var child in item.Children)
            {
                if (MutableChildren.TryGetValue(child, out var existingChild))
                {
                    if (!existingChild.DeepEquals(child) && existingChild is SettingItem childItem)
                    {
                        childItem.Update(child as SettingItem);
                    }
                }
                else
                {
                    MutableChildren.Add(child, child);
                }
            }
        }
    }
}