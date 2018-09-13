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
    public sealed class RepositoryItem : TrustedSignerItem, IEquatable<RepositoryItem>
    {
        public override string ElementName => ConfigurationConstants.Repository;

        public string ServiceIndex => Attributes[ConfigurationConstants.ServiceIndex];

        private readonly OwnersItem _owners;

        public IEnumerable<string> Owners => _owners?.Content ?? Enumerable.Empty<string>();

        protected override HashSet<string> RequiredAttributes => new HashSet<string>() { ConfigurationConstants.NameAttribute, ConfigurationConstants.ServiceIndex };

        public RepositoryItem(string name, string serviceIndex, params CertificateItem[] certificates)
            : this(name, serviceIndex, owners: null, certificates: certificates)
        {
        }

        public RepositoryItem(string name, string serviceIndex, string owners, params CertificateItem[] certificates)
            : base(name, certificates)
        {
            if (string.IsNullOrEmpty(serviceIndex))
            {
                throw new ArgumentNullException(nameof(serviceIndex));
            }

            AddAttribute(ConfigurationConstants.ServiceIndex, serviceIndex);

            if (!string.IsNullOrEmpty(owners))
            {
                _owners = new OwnersItem(owners);
            }
        }

        internal RepositoryItem(XElement element, SettingsFile origin)
            : base(element, origin)
        {
            var parsedOwners = element.Elements().Select(e => SettingFactory.Parse(e, origin)).OfType<OwnersItem>();
            if (parsedOwners != null && parsedOwners.Any())
            {
                if (parsedOwners.Count() > 1)
                {
                    throw new NuGetConfigurationException(
                        string.Format(CultureInfo.CurrentCulture, Resources.UserSettings_UnableToParseConfigFile, Resources.RepositoryMustHaveOneOwners, origin.ConfigFilePath));
                }

                _owners = parsedOwners.FirstOrDefault();
            }
        }

        internal override SettingBase Clone()
        {
            var newItem = new RepositoryItem(
                Name,
                ServiceIndex,
                string.Join(OwnersItem.OwnersListSeparator.ToString(), Owners),
                Certificates.Select(c => c.Clone() as CertificateItem).ToArray());

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

            if (_owners != null)
            {
                element.Add(_owners.AsXNode());
            }

            foreach (var attr in Attributes)
            {
                element.SetAttributeValue(attr.Key, attr.Value);
            }

            return element;
        }

        public bool Equals(RepositoryItem other)
        {
            if (other == null || other.GetType() != GetType())
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return string.Equals(ServiceIndex, other.ServiceIndex, StringComparison.Ordinal);
        }

        public bool DeepEquals(RepositoryItem other)
        {
            if (other == null || other.GetType() != GetType())
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (other.Attributes.Count == Attributes.Count &&
                other.Certificates.Count == Certificates.Count)
            {
                return Attributes.OrderedEquals(other.Attributes, data => data.Key, StringComparer.OrdinalIgnoreCase) &&
                    Certificates.SequenceEqual(other.Certificates) &&
                    Owners.SequenceEqual(other.Owners);
            }

            return false;
        }

        public override int GetHashCode()
        {
            var combiner = new HashCodeCombiner();

            combiner.AddObject(Name);
            combiner.AddObject(ServiceIndex);

            return combiner.CombinedHash;
        }

        public override bool Equals(SettingBase other) => Equals(other as RepositoryItem);
        public override bool DeepEquals(SettingBase other) => DeepEquals(other as RepositoryItem);
        public override bool Equals(object other) => Equals(other as RepositoryItem);

        internal override void SetOrigin(SettingsFile origin)
        {
            base.SetOrigin(origin);

            _owners?.SetOrigin(origin);
        }

        internal override void RemoveFromSettings()
        {
            base.RemoveFromSettings();

            _owners?.RemoveFromSettings();
        }
    }
}
