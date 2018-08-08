// Copyright(c) .NET Foundation.All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace NuGet.Configuration
{
    public abstract class SettingsElement : SettingsNode
    {
        /// <summary>
        /// Text that differentiates element tag
        /// </summary>
        public virtual string Name { get; protected set; }

        /// <summary>
        /// Attributes for xml element
        /// </summary>
        internal IDictionary<string, string> Attributes { get; }

        /// <summary>
        /// Specifies the keys for the attributes that the element can have
        /// </summary>
        /// <remarks>If null then all attributes are allowed</remarks>
        protected virtual HashSet<string> AllowedAttributes => null;

        /// <summary>
        /// Specifies the keys for the attributes that the element should have
        /// </summary>
        /// <remarks>If null or empty then no attributes are required</remarks>
        protected virtual HashSet<string> RequiredAttributes => null;

        /// <summary>
        /// Specifies which values are allowed for a specific attribute.
        /// If an attribute is not defined every value is allowed.
        /// Having allowed values does not imply that the attribute is required.
        /// </summary>
        protected virtual Dictionary<string, HashSet<string>> AllowedValues => null;

        /// <summary>
        /// Specifies values that are explicitely disallowed for a specific attribute.
        /// If an attribute is not defined no value is disallowed.
        /// Having disallowed values does not imply that the attribute is required.
        /// </summary>
        protected virtual Dictionary<string, HashSet<string>> DisallowedValues => null;

        /// <summary>
        /// Specifies if the element is empty.
        /// Each element defines its own definition of empty.
        /// The default definition of empty is an element without attributes.
        /// </summary>
        public override bool IsEmpty() => !Attributes.Any();

        /// <summary>
        /// Default constructor
        /// </summary>
        protected SettingsElement()
            : base()
        {
            Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Constructor used when element is read from a file
        /// </summary>
        /// <param name="element">Xelement read from XML file document tree</param>
        /// <param name="origin">Settings file that this element was read from</param>
        internal SettingsElement(XElement element, ISettingsFile origin)
            : base(element, origin)
        {
            ValidateAttributes(element, origin);

            Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var existingAttribute in element.Attributes())
            {
                Attributes.Add(existingAttribute.Name.LocalName, existingAttribute.Value);
            }
        }

        /// <summary>
        /// Lazily creates the corresponding element as an XNode
        /// </summary>
        public override XNode AsXNode()
        {
            if (Node != null && Node is XElement)
            {
                return Node;
            }

            var element = new XElement(Name);

            foreach (var attr in Attributes)
            {
                element.SetAttributeValue(attr.Key, attr.Value);
            }

            Node = element;

            return Node;
        }

        internal bool TryGetAttributeValue(string key, out string value)
        {
            return Attributes.TryGetValue(key, out value);
        }

        internal bool UpdateAttributeValue(string key, string newValue, bool isBatchOperation = false)
        {
            if (string.IsNullOrEmpty(newValue))
            {
                throw new ArgumentNullException(nameof(newValue));
            }

            if (Origin != null && Origin.IsMachineWide)
            {
                return false;
            }

            if (Attributes.ContainsKey(key))
            {
                if (Node != null && Node is XElement xElement)
                {
                    xElement.SetAttributeValue(key, newValue);
                    Origin.IsDirty = true;

                    if (!isBatchOperation)
                    {
                        Origin.Save();
                    }
                }

                Attributes[key] = newValue;
                return true;
            }

            return false;
        }

        private void ValidateAttributes(XElement element, ISettingsFile origin)
        {
            if (AllowedAttributes != null)
            {
                if (!AllowedAttributes.Any() && element.HasAttributes)
                {
                    throw new NuGetConfigurationException(string.Format(CultureInfo.CurrentCulture, Resources.UserSettings_UnableToParseConfigFile, origin.ConfigFilePath));
                }

                foreach (var attribute in element.Attributes())
                {
                    if (!AllowedAttributes.Contains(attribute.Name.LocalName))
                    {
                        throw new NuGetConfigurationException(string.Format(CultureInfo.CurrentCulture, Resources.UserSettings_UnableToParseConfigFile, origin.ConfigFilePath));
                    }
                }
            }

            if (RequiredAttributes != null)
            {
                foreach (var requireAttribute in RequiredAttributes)
                {
                    var attribute = element.Attribute(requireAttribute);
                    if (attribute == null)
                    {
                        throw new NuGetConfigurationException(string.Format(CultureInfo.CurrentCulture, Resources.UserSettings_UnableToParseConfigFile, origin.ConfigFilePath));
                    }
                }
            }

            if (AllowedValues != null)
            {
                foreach (var attributeValues in AllowedValues)
                {
                    var attribute = element.Attribute(attributeValues.Key);
                    if (attribute != null && !attributeValues.Value.Contains(attribute.Value.Trim()))
                    {
                        throw new NuGetConfigurationException(string.Format(CultureInfo.CurrentCulture, Resources.UserSettings_UnableToParseConfigFile, origin.ConfigFilePath));
                    }
                }
            }

            if (DisallowedValues != null)
            {
                foreach (var attributeValues in DisallowedValues)
                {
                    var attribute = element.Attribute(attributeValues.Key);
                    if (attribute != null && attributeValues.Value.Contains(attribute.Value.Trim()))
                    {
                        throw new NuGetConfigurationException(string.Format(CultureInfo.CurrentCulture, Resources.UserSettings_UnableToParseConfigFile, origin.ConfigFilePath));
                    }
                }
            }
        }
    }
}
