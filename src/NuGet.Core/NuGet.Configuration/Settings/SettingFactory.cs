// Copyright(c) .NET Foundation.All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace NuGet.Configuration
{
    internal static class SettingFactory
    {
        internal static SettingsNode Parse(XNode node, ISettingsFile origin)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            if (node is XText textNode)
            {
                return new SettingsTextContent(textNode, origin);
            }

            if (node is XElement element)
            {
                if (string.Equals(element.Name.LocalName, ConfigurationConstants.Add, StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(element.Parent?.Name.LocalName, ConfigurationConstants.PackageSources, StringComparison.OrdinalIgnoreCase))
                    {
                        return new SourceItem(element, origin);
                    }

                    return new AddItem(element, origin);
                }
                else if (string.Equals(element.Name.LocalName, ConfigurationConstants.Clear, StringComparison.OrdinalIgnoreCase))
                {
                    return new ClearItem(element, origin);
                }
                else if (string.Equals(element.Parent?.Name.LocalName, ConfigurationConstants.CredentialsSectionName, StringComparison.OrdinalIgnoreCase))
                {
                    return new CredentialsItem(element, origin);
                }

                return new SettingsSection(element, origin);
            }

            return null;
        }

        internal static IEnumerable<T> ParseChildren<T>(XElement xElement, ISettingsFile origin, bool canBeCleared) where T : SettingsNode
        {
            var children = new List<T>();             

            var descendants = xElement.Elements().Select(d => Parse(d, origin) as T).Where(c => c != null).Distinct();

            foreach (var descendant in descendants)
            {
                if (descendant is ClearItem)
                {
                    if (canBeCleared)
                    {
                        children.Clear();
                    }

                    children.Add(descendant);

                    continue;
                }

                children.Add(descendant);
            }

            return children;
        }
    }
}