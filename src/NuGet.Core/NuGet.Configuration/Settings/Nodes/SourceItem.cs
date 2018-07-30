// Copyright(c) .NET Foundation.All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Xml.Linq;
using NuGet.Shared;

namespace NuGet.Configuration
{
    public sealed class SourceItem : AddItem
    {
        public override string Value => Settings.ResolvePath(Origin, Attributes[ConfigurationConstants.ValueAttribute]);

        protected override HashSet<string> AllowedAttributes => new HashSet<string>() { ConfigurationConstants.KeyAttribute, ConfigurationConstants.ValueAttribute, ConfigurationConstants.ProtocolVersionAttribute };

        public string ProtocolVersion
        {
            get
            {
                if (Attributes.TryGetValue(ConfigurationConstants.ProtocolVersionAttribute, out var attribute))
                {
                    return Settings.ApplyEnvironmentTransform(attribute);
                }
                return null;
            }
            set => Attributes[ConfigurationConstants.ProtocolVersionAttribute] = value;
        }

        public SourceItem(string key, string value)
            : this(key, value, protocolVersion: "")
        {
        }

        internal SourceItem(XElement element, ISettingsFile origin)
            : base (element, origin)
        {
        }

        public SourceItem(string key, string value, string protocolVersion)
            : base(key, value)
        {
            if (!string.IsNullOrEmpty(protocolVersion))
            {
                ProtocolVersion = protocolVersion;
            }
        }

        public override SettingsItem Copy()
        {
            return new SourceItem(Key, Value, ProtocolVersion);
        }

        public override string GetValueAsPath() => Value;

        public override int GetHashCode()
        {
            var combiner = new HashCodeCombiner();

            combiner.AddObject(Key);

            if (ProtocolVersion != null)
            {
                combiner.AddObject(ProtocolVersion);
            }

            return combiner.CombinedHash;
        }
    }
}
