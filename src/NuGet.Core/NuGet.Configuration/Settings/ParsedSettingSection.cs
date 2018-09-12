// Copyright(c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace NuGet.Configuration
{
    internal sealed class ParsedSettingSection : SettingSection
    {
        internal ParsedSettingSection(XElement element, SettingsFile origin)
            : base(element, origin)
        {
            foreach (var child in ChildrenSet)
            {
                child.Value.Parent = this;
            }
        }

        /// <remarks>
        /// This constructor should only be used when the section is intended to be
        /// added to a specific NuGetConfiguration. A ParsedSettingSection should not
        /// be missing an Origin.
        /// </remarks>
        public ParsedSettingSection(string name, params SettingItem[] children)
            : base(name, attributes: null, children: new HashSet<SettingItem>(children))
        {
        }

        internal override SettingBase Clone()
        {
            return new AbstractSettingSection(ElementName, Attributes, Items.Select(s => s.Clone() as SettingItem));
        }
    }
}
