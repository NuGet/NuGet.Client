// Copyright(c) .NET Foundation.All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Xml.Linq;
using NuGet.Shared;

namespace NuGet.Configuration
{
    public sealed class SourceItem : AddItem
    {
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
            set => AddOrUpdateAttribute(ConfigurationConstants.ProtocolVersionAttribute, value);
        }

        public SourceItem(string key, string value, string protocolVersion = "")
            : base(key, value)
        {
            if (!string.IsNullOrEmpty(protocolVersion))
            {
                ProtocolVersion = protocolVersion;
            }
        }

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

        protected override HashSet<string> AllowedAttributes => new HashSet<string>() { ConfigurationConstants.KeyAttribute, ConfigurationConstants.ValueAttribute, ConfigurationConstants.ProtocolVersionAttribute };

        internal SourceItem(XElement element, SettingsFile origin)
            : base(element, origin)
        {
        }

        internal override SettingBase Clone()
        {
            return new SourceItem(Key, Value, ProtocolVersion)
            {
                Origin = Origin,
            };
        }
    }
}
