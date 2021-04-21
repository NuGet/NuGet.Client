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
    /// <summary>
    /// A PackageNamespacesSourceItem has only a key and at least 1 <see cref="NamespaceItem"/> child item.
    ///     - [Required] Key
    /// </summary>
    public class PackageNamespacesSourceItem : SettingItem
    {
        protected override bool CanHaveChildren => true;

        /// <summary>
        /// List of namespaces items part of this package source namespace element.
        /// </summary>
        public IList<NamespaceItem> Namespaces { get; }

        public override string ElementName => ConfigurationConstants.PackageSourceAttribute;

        /// <summary>
        /// Each PackageSourceNamespaces item needs a key.
        /// The key should correspond a package source key.
        /// </summary>
        public virtual string Key => Attributes[ConfigurationConstants.KeyAttribute];

        protected override IReadOnlyCollection<string> RequiredAttributes { get; } = IReadOnlyCollectionUtility.Create(ConfigurationConstants.KeyAttribute);

        protected void SetKey(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Resources.PropertyCannotBeNullOrEmpty, nameof(Key)));
            }

            UpdateAttribute(ConfigurationConstants.KeyAttribute, value);
        }

        internal readonly IEnumerable<SettingBase> _parsedDescendants;

        /// <summary>
        /// Creates a package source namespace item with the given name, which equals the key and non-empty list of naemspace items.
        /// </summary>
        /// <param name="name">A non-empty name of the item which corresponds a package source name.</param>
        /// <param name="namespaceItems">A non empty list of namespace items.</param>
        /// <exception cref="ArgumentException">If <paramref name="name"/> is null or empty, or <paramref name="namespaceItems"/> is null or empty.</exception>
        public PackageNamespacesSourceItem(string name, IEnumerable<NamespaceItem> namespaceItems)
            : base()
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(name));
            }

            if (namespaceItems == null || !namespaceItems.Any())
            {

                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Resources.Error_ItemNeedsAtLeastOneNamespace, name));
            }

            AddAttribute(ConfigurationConstants.KeyAttribute, name);

            Namespaces = new List<NamespaceItem>();

            foreach (NamespaceItem @namespace in namespaceItems)
            {
                Namespaces.Add(@namespace);
            }
        }

        internal PackageNamespacesSourceItem(XElement element, SettingsFile origin)
            : base(element, origin)
        {
            _parsedDescendants = element.Nodes().Where(n => n is XElement || n is XText text && !string.IsNullOrWhiteSpace(text.Value))
                .Select(e => SettingFactory.Parse(e, origin));

            var parsedNamespaceItems = _parsedDescendants.OfType<NamespaceItem>().ToList();

            if (parsedNamespaceItems.Count == 0)
            {
                throw new NuGetConfigurationException(string.Format(CultureInfo.CurrentCulture, Resources.Error_ItemNeedsAtLeastOneNamespaceWithPath, Key, origin.ConfigFilePath));
            }

            Namespaces = parsedNamespaceItems;
        }

        internal override void SetOrigin(SettingsFile origin)
        {
            base.SetOrigin(origin);

            foreach (NamespaceItem @namespace in Namespaces)
            {
                @namespace.SetOrigin(origin);
            }
        }

        internal override void RemoveFromSettings()
        {
            base.RemoveFromSettings();

            foreach (NamespaceItem @namespace in Namespaces)
            {
                @namespace.RemoveFromSettings();
            }
        }

        public override SettingBase Clone()
        {
            var newItem = new PackageNamespacesSourceItem(
                Key,
                Namespaces.Select(c => c.Clone() as NamespaceItem).ToArray());

            if (Origin != null)
            {
                newItem.SetOrigin(Origin);
            }

            return newItem;
        }

        internal override XNode AsXNode()
        {
            if (Node is XElement)
            {
                return Node;
            }

            var element = new XElement(ElementName);

            foreach (NamespaceItem packageNamespaceItem in Namespaces)
            {
                element.Add(packageNamespaceItem.AsXNode());
            }

            foreach (KeyValuePair<string, string> attr in Attributes)
            {
                element.SetAttributeValue(attr.Key, attr.Value);
            }

            return element;
        }

        internal override void Update(SettingItem other)
        {
            var packageSourceNamespaces = other as PackageNamespacesSourceItem;

            if (!packageSourceNamespaces.Namespaces.Any())
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Resources.Error_ItemNeedsAtLeastOneNamespace, packageSourceNamespaces.Key));
            }

            base.Update(other);

            Dictionary<NamespaceItem, NamespaceItem> otherNamespaces = packageSourceNamespaces.Namespaces.ToDictionary(c => c, c => c);
            var immutableNamespaces = new List<NamespaceItem>(Namespaces);
            foreach (NamespaceItem namespaceItem in immutableNamespaces)
            {
                if (otherNamespaces.TryGetValue(namespaceItem, out NamespaceItem otherChild))
                {
                    otherNamespaces.Remove(namespaceItem);
                }

                if (otherChild == null)
                {
                    Namespaces.Remove(namespaceItem);
                    namespaceItem.RemoveFromSettings();
                }
                else if (namespaceItem is SettingItem item)
                {
                    item.Update(otherChild);
                }
            }

            foreach (var newNamespaceItem in otherNamespaces)
            {
                var itemToAdd = newNamespaceItem.Value;
                Namespaces.Add(itemToAdd);

                if (Origin != null)
                {
                    itemToAdd.SetOrigin(Origin);

                    if (Node != null)
                    {
                        itemToAdd.SetNode(itemToAdd.AsXNode());

                        XElementUtility.AddIndented(Node as XElement, itemToAdd.Node);
                        Origin.IsDirty = true;
                    }
                }
            }
        }

        public override bool Equals(object other)
        {
            // It is important that equality on checks that the namespace is for the same `key. The content is not important. 
            // The equality here is used for updating namespaces.
            if (other is PackageNamespacesSourceItem item)
            {
                if (ReferenceEquals(this, item))
                {
                    return true;
                }

                return string.Equals(Key, item.Key, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        public override int GetHashCode()
        {
            var combiner = new HashCodeCombiner();

            combiner.AddStringIgnoreCase(Key);

            return combiner.CombinedHash;
        }
    }
}
