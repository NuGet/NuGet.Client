// Copyright(c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace NuGet.Configuration
{
    internal sealed class NuGetConfiguration : SettingsGroup<SettingSection>, IEquatable<NuGetConfiguration>, ISettingsGroup
    {
        public override string ElementName => ConfigurationConstants.Configuration;

        internal IReadOnlyDictionary<string, SettingSection> Sections => ChildrenSet.Select(c => c.Value).ToDictionary(c => c.ElementName);

        protected override bool CanBeCleared => false;

        /// <remarks>
        /// There should not be a NuGetConfiguration without an Origin.
        /// This constructor should only be used for tests.
        /// </remarks>
        private NuGetConfiguration(IReadOnlyDictionary<string, string> attributes, IEnumerable<SettingSection> children)
            : base(attributes, children)
        {
        }

        /// <remarks>
        /// There should not be a NuGetConfiguration without an Origin.
        /// This constructor should only be used for tests.
        /// </remarks>
        internal NuGetConfiguration(params SettingSection[] sections)
            : base()
        {
            foreach (var section in sections)
            {
                section.Parent = this;
                ChildrenSet.Add(section, section);
            }
        }

        internal NuGetConfiguration(SettingsFile origin)
            : base()
        {
            var defaultSource = new SourceItem(NuGetConstants.FeedName, NuGetConstants.V3FeedUrl, protocolVersion: PackageSourceProvider.MaxSupportedProtocolVersion.ToString());

            defaultSource.SetNode(defaultSource.AsXNode());

            var defaultSection = new ParsedSettingSection(ConfigurationConstants.PackageSources, defaultSource)
            {
                Parent = this
            };

            defaultSection.SetNode(defaultSection.AsXNode());

            ChildrenSet.Add(defaultSection, defaultSection);

            SetNode(AsXNode());
            SetOrigin(origin);
        }

        internal NuGetConfiguration(XElement element, SettingsFile origin)
            : base(element, origin)
        {
            if (!string.Equals(element.Name.LocalName, ElementName, StringComparison.OrdinalIgnoreCase))
            {
                throw new NuGetConfigurationException(
                         string.Format(Resources.ShowError_ConfigRootInvalid, origin.ConfigFilePath));
            }
        }

        public void AddOrUpdate(string sectionName, SettingItem item)
        {
            if (string.IsNullOrEmpty(sectionName))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(sectionName));
            }

            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            if (Sections.TryGetValue(sectionName, out var section))
            {
                // section exists, update or add the element on it
                if (section.Update(item) || section.Add(item))
                {
                    return;
                }
            }

            // The section is new, add it with the item
            Add(new ParsedSettingSection(sectionName, item));
        }

        public void Remove(string sectionName, SettingItem item)
        {
            if (string.IsNullOrEmpty(sectionName))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(sectionName));
            }

            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            if (Sections.TryGetValue(sectionName, out var section))
            {
                section.Remove(item);
            }
        }

        public SettingSection GetSection(string sectionName)
        {
            if (Sections.TryGetValue(sectionName, out var section))
            {
                return section.Clone() as SettingSection;
            }

            return null;
        }

        internal void MergeSectionsInto(Dictionary<string, VirtualSettingSection> sectionsContainer)
        {
            // loop through the current element's sections: merge any overlapped sections, add any missing section
            foreach (var section in Sections)
            {
                if (sectionsContainer.TryGetValue(section.Value.ElementName, out var settingsSection))
                {
                    settingsSection.Merge(section.Value);
                }
                else
                {
                    sectionsContainer.Add(section.Key, new VirtualSettingSection(section.Value));
                }
            }
        }

        internal override SettingBase Clone()
        {
            return new NuGetConfiguration(Attributes, Sections.Select(s => s.Value.Clone() as SettingSection));
        }

        public bool Equals(NuGetConfiguration other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Sections.SequenceEqual(other.Sections);
        }

        public bool DeepEquals(NuGetConfiguration other) => Equals(other);
        public override bool DeepEquals(SettingBase other) => Equals(other as NuGetConfiguration);
        public override bool Equals(SettingBase other) => Equals(other as NuGetConfiguration);
        public override bool Equals(object other) => Equals(other as NuGetConfiguration);
        public override int GetHashCode() => ChildrenSet.GetHashCode();
    }
}
