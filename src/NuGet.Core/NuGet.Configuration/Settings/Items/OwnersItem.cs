// Copyright(c) .NET Foundation.All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace NuGet.Configuration
{
    public sealed class OwnersItem : SettingItem, IEquatable<OwnersItem>
    {
        public static readonly char OwnersListSeparator = ';';

        public override string ElementName => ConfigurationConstants.Owners;

        protected override bool CanHaveChildren => true;

        public IList<string> Content => _content.Value.Split(OwnersListSeparator).Select(o => o.Trim()).ToList();

        private readonly SettingText _content;

        public OwnersItem(string owners)
            : base()
        {
            if (string.IsNullOrEmpty(owners))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(owners));
            }

            _content = new SettingText(owners);
        }

        internal OwnersItem(XElement element, SettingsFile origin)
            : base(element, origin)
        {
            var descendants = element.Nodes().Where(n => n is XText text && !string.IsNullOrWhiteSpace(text.Value) || n is XElement)
                .Select(d => SettingFactory.Parse(d, origin)).Distinct();

            if (descendants == null || descendants.Count() != 1 || descendants.FirstOrDefault(d => d is SettingText) == null)
            {
                throw new NuGetConfigurationException(
                    string.Format(CultureInfo.CurrentCulture, Resources.UserSettings_UnableToParseConfigFile, Resources.OwnersMustOnlyHaveContent, origin.ConfigFilePath));
            }

            _content = descendants.OfType<SettingText>().First();
        }

        internal override SettingBase Clone()
        {
            var newItem = new OwnersItem(_content.Value);

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

            var element = new XElement(ElementName, _content.AsXNode());

            foreach (var attr in Attributes)
            {
                element.SetAttributeValue(attr.Key, attr.Value);
            }

            return element;
        }

        public bool Equals(OwnersItem other)
        {
            if (other == null || other.GetType() != GetType())
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Content.SequenceEqual(other.Content, StringComparer.Ordinal);
        }

        public bool DeepEquals(OwnersItem other) => Equals(other);
        public override bool Equals(SettingBase other) => Equals(other as OwnersItem);
        public override bool DeepEquals(SettingBase other) => DeepEquals(other as OwnersItem);
        public override bool Equals(object other) => Equals(other as OwnersItem);
        public override int GetHashCode() => Content.GetHashCode();
    }
}
