// Copyright(c) .NET Foundation.All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using NuGet.Shared;

namespace NuGet.Configuration
{
    public sealed class UnknownItem : SettingsItem, ISettingsCollection, IEquatable<UnknownItem>
    {
        public override string Name { get; protected set; }

        public IList<SettingsItem> Children { get; private set; }

        protected override bool CanHaveChildren => true;

        public override bool IsEmpty() => false;

        internal UnknownItem(XElement element, ISettingsFile origin)
            : base(element, origin)
        {
            Name = element.Name.LocalName;
            Children = SettingFactory.ParseChildren<SettingsItem>(element, origin, canBeCleared: false).Where(c => c != null).ToList();
        }

        public UnknownItem(string name, IDictionary<string, string> attributes, IEnumerable<SettingsItem> children)
            : base()
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            Name = name;

            if (attributes != null)
            {
                foreach (var attribute in attributes)
                {
                    Attributes.Add(attribute);
                }
            }

            if (children != null)
            {
                Children = children.ToList();
            }
        }

        public override SettingsItem Copy() => new UnknownItem(Name, Attributes, Children);

        public override bool Update(SettingsItem item)
        {
            if (base.Update(item) && item is UnknownItem unknown)
            {
                Children = unknown.Children;

                var element = Node as XElement;
                if (element != null)
                {
                    element.RemoveNodes();

                    foreach (var child in Children)
                    {
                        XElementUtility.AddIndented(element, child.AsXNode());
                    }
                }

                return true;
            }

            return false;
        }

        public bool AddChild(SettingsNode child, bool isBatchOperation = false)
        {
            return AddChild(child as SettingsItem, isBatchOperation);
        }

        public bool AddChild(SettingsItem child, bool isBatchOperation = false)
        {
            if (child != null && !child.IsEmpty())
            {
                if (!child.IsAbstract)
                {
                    throw new InvalidOperationException(
                        string.Format(CultureInfo.CurrentCulture, Resources.ShowError_AddChildAlreadyHasOrigin, child.Origin.ConfigFilePath));
                }

                if (Origin != null && Origin.IsMachineWide)
                {
                    return false;
                }

                if (!Children.Contains(child))
                {
                    Children.Add(child);

                    if (Origin != null)
                    {
                        child.AddToOrigin(Origin);

                        XElementUtility.AddIndented(Node as XElement, child.AsXNode());
                        Origin.IsDirty = true;

                        if (!isBatchOperation)
                        {
                            Origin.Save();
                        }
                    }

                    child.Parent = this;

                    return true;
                }
            }

            return false;
        }

        public bool RemoveChild(SettingsNode child, bool isBatchOperation = false)
        {
            return RemoveChild(child as SettingsItem, isBatchOperation);
        }

        public bool RemoveChild(SettingsItem child, bool isBatchOperation = false)
        {
            if (child == null || (child.Origin != null && child.Origin.IsMachineWide))
            {
                return false;
            }

            if (Children.Contains(child) && Children.Remove(child))
            {
                child.Parent = null;
                var successfullyRemoved = true;

                if (!child.IsAbstract)
                {
                    XElementUtility.RemoveIndented(child.Node);
                    child.Origin.IsDirty = true;

                    if (!isBatchOperation)
                    {
                        child.Origin.Save();
                    }

                    child.Origin = null;
                    child.Node = null;
                }

                if (child.MergedWith != null)
                {
                    successfullyRemoved &= child.MergedWith.RemoveFromCollection(isBatchOperation);
                }

                if (Parent != null && IsEmpty())
                {
                    successfullyRemoved &= RemoveFromCollection(isBatchOperation);
                }

                return successfullyRemoved;
            }

            return false;
        }

        public override XNode AsXNode()
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

            Node = element;

            return Node;
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

        public override bool Equals(SettingsNode other) => Equals(other as UnknownItem);
        public override bool DeepEquals(SettingsNode other) => DeepEquals(other as UnknownItem);
        public override bool Equals(object other) => Equals(other as UnknownItem);
        public override int GetHashCode() => Name.GetHashCode();
    }
}
