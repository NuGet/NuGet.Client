// Copyright(c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Xml.Linq;
using NuGet.Shared;

namespace NuGet.Configuration
{
    public class AddItem : SettingItem, IEquatable<AddItem>
    {
        public override string Name => ConfigurationConstants.Add;

        public string Key => Attributes[ConfigurationConstants.KeyAttribute];

        public virtual string Value
        {
            get => Settings.ApplyEnvironmentTransform(Attributes[ConfigurationConstants.ValueAttribute]);
            set => AddOrUpdateAttribute(ConfigurationConstants.ValueAttribute, value);
        }

        public IReadOnlyDictionary<string, string> AdditionalAttributes => new ReadOnlyDictionary<string, string>(
            Attributes.Where(a =>
                !string.Equals(a.Key, ConfigurationConstants.KeyAttribute, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(a.Key, ConfigurationConstants.ValueAttribute, StringComparison.OrdinalIgnoreCase)
            ).ToDictionary(a => a.Key, a => a.Value));

        public AddItem(string key, string value)
            : this(key, value, additionalAttributes: null)
        {
        }

        public AddItem(string key, string value, IReadOnlyDictionary<string, string> additionalAttributes)
            : base()
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentNullException(nameof(key));
            }

            AddAttribute(ConfigurationConstants.KeyAttribute, key);
            AddAttribute(ConfigurationConstants.ValueAttribute, value ?? string.Empty);

            if (additionalAttributes != null)
            {
                foreach (var attribute in additionalAttributes)
                {
                    AddAttribute(attribute.Key, attribute.Value);
                }
            }
        }

        public virtual string GetValueAsPath()
        {
            if (Origin != null)
            {
                return Settings.ResolvePathFromOrigin(Origin.DirectoryPath, Origin.ConfigFilePath, Value);
            }

            return Value;
        }

        public void AddOrUpdateAdditionalAttribute(string attributeName, string value)
        {
            if (Origin != null && Origin.IsMachineWide)
            {
                return;
            }

            if (string.Equals(ConfigurationConstants.KeyAttribute, attributeName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ConfigurationConstants.ValueAttribute, attributeName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (Attributes.ContainsKey(attributeName))
            {
                UpdateAttribute(attributeName, value);
            }
            else
            {
                AddAttribute(attributeName, value);
            }
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

        public override bool Equals(SettingBase other) =>  Equals(other as AddItem);
        public override bool DeepEquals(SettingBase other) => DeepEquals(other as AddItem);
        public override bool Equals(object other) => Equals(other as AddItem);
        public override int GetHashCode() => Key.GetHashCode();

        protected override HashSet<string> RequiredAttributes => new HashSet<string>() { ConfigurationConstants.KeyAttribute, ConfigurationConstants.ValueAttribute };

        protected override Dictionary<string, HashSet<string>> DisallowedValues => new Dictionary<string, HashSet<string>>()
        {
            { ConfigurationConstants.KeyAttribute, new HashSet<string>() { string.Empty } }
        };

        internal AddItem(XElement element, SettingsFile origin)
            : base(element, origin)
        {
        }

        internal override SettingBase Clone()
        {
            return new AddItem(Key, Value, AdditionalAttributes)
            {
                Origin = Origin
            };
        }

        internal override void Update(SettingItem other)
        {
            base.Update(other);

            if ((!other.Attributes.TryGetValue(ConfigurationConstants.ValueAttribute, out var value) ||
                string.IsNullOrEmpty(value)) && Parent != null)
            {
                Parent.Remove(this);
            }
        }
    }
}
