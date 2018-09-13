// Copyright(c) .NET Foundation.All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using NuGet.Shared;

namespace NuGet.Configuration
{
    public sealed class AuthorItem : TrustedSignerItem, IEquatable<AuthorItem>
    {
        public override string ElementName => ConfigurationConstants.Author;

        protected override HashSet<string> RequiredAttributes => new HashSet<string>() { ConfigurationConstants.NameAttribute };

        public AuthorItem(string name, params CertificateItem[] certificates)
            : base(name, certificates)
        {
        }

        internal AuthorItem(XElement element, SettingsFile origin)
            : base(element, origin)
        {
        }

        internal override SettingBase Clone()
        {
            var newItem = new AuthorItem(Name, Certificates.ToArray());

            if (Origin != null)
            {
                newItem.SetOrigin(Origin);
            }

            return newItem;
        }

        internal override XNode AsXNode()
        {
            if (Node != null && Node is XElement)
            {
                return Node;
            }

            var element = new XElement(ElementName);

            foreach (var cert in Certificates)
            {
                element.Add(cert.AsXNode());
            }

            foreach (var attr in Attributes)
            {
                element.SetAttributeValue(attr.Key, attr.Value);
            }

            return element;
        }

        public bool Equals(AuthorItem other)
        {
            if (other == null || other.GetType() != GetType())
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return string.Equals(Name, other.Name, StringComparison.Ordinal);
        }

        public bool DeepEquals(AuthorItem other)
        {
            if (other == null || other.GetType() != GetType())
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (other.Attributes.Count == Attributes.Count)
            {
                return Attributes.OrderedEquals(other.Attributes, data => data.Key, StringComparer.OrdinalIgnoreCase) &&
                    Certificates.SequenceEqual(other.Certificates);
            }

            return false;
        }

        public override bool Equals(SettingBase other) => Equals(other as AuthorItem);
        public override bool DeepEquals(SettingBase other) => DeepEquals(other as AuthorItem);
        public override bool Equals(object other) => Equals(other as AuthorItem);
        public override int GetHashCode() => Name.GetHashCode();
    }
}
