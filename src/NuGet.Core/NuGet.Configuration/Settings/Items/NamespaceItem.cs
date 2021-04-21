// Copyright(c) .NET Foundation.All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;

namespace NuGet.Configuration
{
    /// <summary>
    /// A NamespaceItem has only a key and no children.
    ///     - [Required] Id
    /// </summary>
    public sealed class NamespaceItem : SettingItem
    {
        public override string ElementName => ConfigurationConstants.Namespace;

        public string Id => Attributes[ConfigurationConstants.IdAttribute];

        protected override IReadOnlyCollection<string> RequiredAttributes { get; }
                = IReadOnlyCollectionUtility.Create(ConfigurationConstants.IdAttribute);

        public NamespaceItem(string id)
            : base()
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(id));
            }

            AddAttribute(ConfigurationConstants.IdAttribute, id);
        }

        internal NamespaceItem(XElement element, SettingsFile origin)
            : base(element, origin)
        {
        }

        public override SettingBase Clone()
        {
            var newItem = new NamespaceItem(Id);

            if (Origin != null)
            {
                newItem.SetOrigin(Origin);
            }

            return newItem;
        }

        public override bool Equals(object other)
        {
            if (other is NamespaceItem item)
            {
                if (ReferenceEquals(this, item))
                {
                    return true;
                }

                return string.Equals(Id, item.Id, StringComparison.Ordinal);
            }

            return false;
        }

        public override int GetHashCode() => Id.GetHashCode();
    }
}
