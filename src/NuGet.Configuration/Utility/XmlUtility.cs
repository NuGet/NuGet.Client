// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace NuGet.Configuration
{
    internal static class XmlUtility
    {
        internal static XDocument GetOrCreateDocument(XName rootName, string fullPath)
        {
            if (File.Exists(fullPath))
            {
                try
                {
                    return GetDocument(fullPath);
                }
                catch (FileNotFoundException)
                {
                    return CreateDocument(rootName, fullPath);
                }
            }
            return CreateDocument(rootName, fullPath);
        }

        private static XDocument CreateDocument(XName rootName, string fullPath)
        {
            var document = new XDocument(new XElement(rootName));
            // Add it to the file system
            FileSystemUtility.AddFile(fullPath, document.Save);
            return document;
        }

        private static XDocument GetDocument(string fullPath)
        {
            using (Stream configStream = File.OpenRead(fullPath))
            {
                return LoadSafe(configStream, LoadOptions.PreserveWhitespace);
            }
        }

        private static XDocument LoadSafe(Stream input, LoadOptions options)
        {
            var settings = CreateSafeSettings();
            var reader = XmlReader.Create(input, settings);
            return XDocument.Load(reader, options);
        }

        private static XmlReaderSettings CreateSafeSettings(bool ignoreWhiteSpace = false)
        {
            var safeSettings = new XmlReaderSettings
                {
#if !DNXCORE50
                    XmlResolver = null,
#endif
                    DtdProcessing = DtdProcessing.Prohibit,
                    IgnoreWhitespace = ignoreWhiteSpace
                };

            return safeSettings;
        }
    }

    internal static class XElementUtility
    {
        internal static string GetOptionalAttributeValue(XElement element, string localName, string namespaceName = null)
        {
            XAttribute attr;
            if (String.IsNullOrEmpty(namespaceName))
            {
                attr = element.Attribute(localName);
            }
            else
            {
                attr = element.Attribute(XName.Get(localName, namespaceName));
            }
            return attr != null ? attr.Value : null;
        }

        internal static void AddIndented(XContainer container, XContainer content)
        {
            var oneIndentLevel = ComputeOneLevelOfIndentation(container);

            var leadingText = container.PreviousNode as XText;
            var parentIndent = leadingText != null ? leadingText.Value : Environment.NewLine;

            IndentChildrenElements(content, parentIndent + oneIndentLevel, oneIndentLevel);

            AddLeadingIndentation(container, parentIndent, oneIndentLevel);
            container.Add(content);
            AddTrailingIndentation(container, parentIndent);
        }

        internal static void RemoveIndented(XNode element)
        {
            // NOTE: this method is tested by BindinRedirectManagerTest and SettingsTest
            var textBeforeOrNull = element.PreviousNode as XText;
            var textAfterOrNull = element.NextNode as XText;
            var oneIndentLevel = ComputeOneLevelOfIndentation(element);
            var isLastChild = !element.ElementsAfterSelf().Any();

            element.Remove();

            if (textAfterOrNull != null
                && IsWhiteSpace(textAfterOrNull))
            {
                textAfterOrNull.Remove();
            }

            if (isLastChild
                && textBeforeOrNull != null
                && IsWhiteSpace(textAfterOrNull))
            {
                textBeforeOrNull.Value = textBeforeOrNull.Value.Substring(0, textBeforeOrNull.Value.Length - oneIndentLevel.Length);
            }
        }

        private static string ComputeOneLevelOfIndentation(XNode node)
        {
            var depth = node.Ancestors().Count();
            var textBeforeOrNull = node.PreviousNode as XText;
            if (depth == 0
                || textBeforeOrNull == null
                || !IsWhiteSpace(textBeforeOrNull))
            {
                return "  ";
            }

            var indentString = textBeforeOrNull.Value.Trim(Environment.NewLine.ToCharArray());
            var lastChar = indentString.LastOrDefault();
            var indentChar = (lastChar == '\t' ? '\t' : ' ');
            var indentLevel = Math.Max(1, indentString.Length / depth);
            return new string(indentChar, indentLevel);
        }

        private static bool IsWhiteSpace(XText textNode)
        {
            return string.IsNullOrWhiteSpace(textNode.Value);
        }

        private static void IndentChildrenElements(XContainer container, string containerIndent, string oneIndentLevel)
        {
            var childIndent = containerIndent + oneIndentLevel;
            foreach (var element in container.Elements())
            {
                element.AddBeforeSelf(new XText(childIndent));
                IndentChildrenElements(element, childIndent + oneIndentLevel, oneIndentLevel);
            }

            if (container.Elements().Any())
            {
                container.Add(new XText(containerIndent));
            }
        }

        private static void AddLeadingIndentation(XContainer container, string containerIndent, string oneIndentLevel)
        {
            var containerIsSelfClosed = !container.Nodes().Any();
            var lastChildText = container.LastNode as XText;
            if (containerIsSelfClosed || lastChildText == null)
            {
                container.Add(new XText(containerIndent + oneIndentLevel));
            }
            else
            {
                lastChildText.Value += oneIndentLevel;
            }
        }

        private static void AddTrailingIndentation(XContainer container, string containerIndent)
        {
            container.Add(new XText(containerIndent));
        }
    }
}
