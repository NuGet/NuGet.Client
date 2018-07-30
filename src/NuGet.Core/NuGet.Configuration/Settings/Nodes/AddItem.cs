// Copyright(c) .NET Foundation.All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using NuGet.Shared;

namespace NuGet.Configuration
{
    public class AddItem : SettingsItem, IEquatable<AddItem>
    {
        public override string Name => ConfigurationConstants.Add;

        public string Key => Attributes[ConfigurationConstants.KeyAttribute];

        public virtual string Value => Settings.ApplyEnvironmentTransform(Attributes[ConfigurationConstants.ValueAttribute]);

        protected override HashSet<string> RequiredAttributes => new HashSet<string>() { ConfigurationConstants.KeyAttribute, ConfigurationConstants.ValueAttribute };

        internal AddItem(XElement element, ISettingsFile origin)
            : base(element, origin)
        {
        }

        public AddItem(string key, string value)
            : this(key, value, additionalAttributes: null)
        {
        }

        public AddItem(string key, string value, IDictionary<string, string> additionalAttributes)
            : base()
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentNullException(nameof(key));
            }

            Attributes.Add(ConfigurationConstants.KeyAttribute, key);
            Attributes.Add(ConfigurationConstants.ValueAttribute, value);

            if (additionalAttributes != null)
            {
                foreach (var attribute in additionalAttributes)
                {
                    Attributes.Add(attribute);
                }
            }
        }

        public virtual string GetValueAsPath()
        {
            return Settings.ResolvePath(Origin, Value);
        }

        public override SettingsItem Copy()
        {
            var additionalAttributes = Attributes.Where(p => p.Key != ConfigurationConstants.KeyAttribute && p.Key != ConfigurationConstants.ValueAttribute).ToDictionary(p => p.Key, p => p.Value);
            return new AddItem(Key, Value, additionalAttributes);
        }

        public bool Equals(AddItem other)
        {
            if (other == null || other.GetType() != GetType())
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return string.Equals(Key, other.Key, StringComparison.Ordinal);
        }

        public bool DeepEquals(AddItem other)
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
                return Attributes.OrderedEquals(other.Attributes, data => data.Key, StringComparer.OrdinalIgnoreCase);
            }

            return false;
        }

        public override bool Equals(SettingsNode other) =>  Equals(other as AddItem);
        public override bool DeepEquals(SettingsNode other) => DeepEquals(other as AddItem);
        public override bool Equals(object other) => Equals(other as AddItem);
        public override int GetHashCode() => Key.GetHashCode();
    }
}
