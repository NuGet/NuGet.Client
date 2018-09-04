// Copyright(c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;
using NuGet.Shared;

namespace NuGet.Configuration
{
    public sealed class UnknownItem : SettingItem, ISettingsGroup, IEquatable<UnknownItem>
    {
        public override string Name { get; protected set; }

        public IReadOnlyList<SettingItem> Children => MutableChildren.AsReadOnly();

        public new IReadOnlyDictionary<string, string> Attributes => base.Attributes;

        internal override bool IsEmpty() => false;

        protected override bool CanHaveChildren => true;

        private List<SettingItem> MutableChildren { get; set; }

        internal UnknownItem(XElement element, SettingsFile origin)
            : base(element, origin)
        {
            Name = element.Name.LocalName;
            MutableChildren = SettingFactory.ParseChildren<SettingItem>(element, origin, canBeCleared: false).Where(c => c != null).ToList();

            foreach (var child in MutableChildren)
            {
                child.Parent = this;
            }
        }

        public UnknownItem(string name, IReadOnlyDictionary<string, string> attributes, IEnumerable<SettingItem> children)
            : base(attributes)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            Name = name;

            if (children != null)
            {
                MutableChildren = children.ToList();

                foreach (var child in MutableChildren)
                {
                    child.Parent = this;
                }
            }
        }

        internal override SettingBase Clone() => new UnknownItem(Name, Attributes, Children) { Origin = Origin };

        internal bool Add(SettingItem setting)
        {
            if (setting == null)
            {
                throw new ArgumentNullException(nameof(setting));
            }

            Debug.Assert(!IsAbstract());

            if (Origin.IsMachineWide)
            {
                throw new InvalidOperationException(Resources.CannotUpdateMachineWide);
            }

            Debug.Assert(setting.IsAbstract());

            if (!MutableChildren.Contains(setting) && !setting.IsEmpty())
            {
                MutableChildren.Add(setting);

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
            Remove(setting as SettingItem);
        }

        private void Remove(SettingItem setting)
        {
            if (setting == null)
            {
                throw new ArgumentNullException(nameof(setting));
            }

            Debug.Assert(!IsAbstract());
            Debug.Assert(setting.Origin == Origin);

            if (Origin != null && Origin.IsMachineWide)
            {
                throw new InvalidOperationException(Resources.CannotUpdateMachineWide);
            }

            var currentSetting = MutableChildren.Find(c => c.Equals(setting));
            if (currentSetting != null && MutableChildren.Remove(currentSetting))
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

            var otherChildren = unknown.MutableChildren.ToDictionary(i => i, i => i);
            var childrenImmutable = new List<SettingItem>(Children);
            foreach (var child in childrenImmutable)
            {
                if (otherChildren.TryGetValue(child, out var otherChild))
                {
                    otherChildren.Remove(child);
                }

                if (otherChild == null)
                {
                    Remove(child);
                }
                else if (!child.DeepEquals(otherChild))
                {
                    child.Update(otherChild);
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
                if (!Attributes.ContainsKey(attribute.Key))
                {
                    AddAttribute(attribute.Key, attribute.Value);
                }
            }

            foreach (var child in item.Children)
            {
                if (!Children.Contains(child))
                {
                    if (!MutableChildren.Contains(child))
                    {
                        MutableChildren.Add(child);
                    }
                }
            }
        }
    }
}