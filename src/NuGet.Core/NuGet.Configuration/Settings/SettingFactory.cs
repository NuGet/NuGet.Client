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
        internal static SettingBase Parse(XNode node, SettingsFile origin)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            if (node is XText textNode)
            {
                return new SettingText(textNode, origin);
            }

            if (node is XElement element)
            {
                var elementType = SettingElementType.Unknown;
                Enum.TryParse(element.Name.LocalName, ignoreCase: true, result: out elementType);

                var parentType = SettingElementType.Unknown;
                if (element.Parent != null)
                {
                    Enum.TryParse(element.Parent?.Name.LocalName, ignoreCase: true, result: out parentType);
                }

                switch (parentType)
                {
                    case SettingElementType.Configuration:
                        return new ParsedSettingSection(element, origin);

                    case SettingElementType.PackageSourceCredentials:
                        return new CredentialsItem(element, origin);

                    case SettingElementType.PackageSources:
                        if (elementType == SettingElementType.Add)
                        {
                            return new SourceItem(element, origin);
                        }

                        break;
                }

                switch (elementType)
                {
                    case SettingElementType.Add:
                        return new AddItem(element, origin);

                    case SettingElementType.Author:
                        return new AuthorItem(element, origin);

                    case SettingElementType.Certificate:
                        return new CertificateItem(element, origin);

                    case SettingElementType.Clear:
                        return new ClearItem(element, origin);

                    case SettingElementType.Owners:
                        return new OwnersItem(element, origin);

                    case SettingElementType.Repository:
                        return new RepositoryItem(element, origin);

                    case SettingElementType.FileCert:
                        return new FileClientCertItem(element, origin);

                    case SettingElementType.StoreCert:
                        return new StoreClientCertItem(element, origin);
                }

                return new UnknownItem(element, origin);
            }

            return null;
        }

        internal static IEnumerable<T> ParseChildren<T>(XElement xElement, SettingsFile origin, bool canBeCleared) where T : SettingElement
        {
            var children = new List<T>();

            var descendants = xElement.Elements().Select(d => Parse(d, origin)).OfType<T>().Distinct();

            foreach (var descendant in descendants)
            {
                if (canBeCleared && descendant is ClearItem)
                {
                    children.Clear();
                }

                children.Add(descendant);
            }

            return children;
        }
    }
}
