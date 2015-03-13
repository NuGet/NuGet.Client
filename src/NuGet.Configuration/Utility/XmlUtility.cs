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
            XDocument document = new XDocument(new XElement(rootName));
            // Add it to the file system
            FileSystemUtility.AddFile(fullPath, document.Save);
            return document;
        }

        private static XDocument GetDocument(string fullPath)
        {
            using (Stream configStream = File.OpenRead(fullPath))
            {
                return XmlUtility.LoadSafe(configStream, LoadOptions.PreserveWhitespace);
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
            string oneIndentLevel = ComputeOneLevelOfIndentation(container);

            XText leadingText = container.PreviousNode as XText;
            string parentIndent = leadingText != null ? leadingText.Value : Environment.NewLine;

            XElementUtility.IndentChildrenElements(content, parentIndent + oneIndentLevel, oneIndentLevel);

            XElementUtility.AddLeadingIndentation(container, parentIndent, oneIndentLevel);
            container.Add(content);
            AddTrailingIndentation(container, parentIndent);
        }

        internal static void RemoveIndented(XNode element)
        {
            // NOTE: this method is tested by BindinRedirectManagerTest and SettingsTest
            XText textBeforeOrNull = element.PreviousNode as XText;
            XText textAfterOrNull = element.NextNode as XText;
            string oneIndentLevel = ComputeOneLevelOfIndentation(element);
            bool isLastChild = !element.ElementsAfterSelf().Any();

            element.Remove();

            if (textAfterOrNull != null && IsWhiteSpace(textAfterOrNull))
                textAfterOrNull.Remove();

            if (isLastChild && textBeforeOrNull != null && IsWhiteSpace(textAfterOrNull))
                textBeforeOrNull.Value = textBeforeOrNull.Value.Substring(0, textBeforeOrNull.Value.Length - oneIndentLevel.Length);
        }

        private static string ComputeOneLevelOfIndentation(XNode node)
        {
            var depth = node.Ancestors().Count();
            XText textBeforeOrNull = node.PreviousNode as XText;
            if (depth == 0 || textBeforeOrNull == null || !IsWhiteSpace(textBeforeOrNull))
                return "  ";

            string indentString = textBeforeOrNull.Value.Trim(Environment.NewLine.ToCharArray());
            char lastChar = indentString.LastOrDefault();
            char indentChar = (lastChar == '\t' ? '\t' : ' ');
            int indentLevel = Math.Max(1, indentString.Length / depth);
            return new string(indentChar, indentLevel);
        }

        private static bool IsWhiteSpace(XText textNode)
        {
            return string.IsNullOrWhiteSpace(textNode.Value);
        }

        private static void IndentChildrenElements(XContainer container, string containerIndent, string oneIndentLevel)
        {
            string childIndent = containerIndent + oneIndentLevel;
            foreach (XElement element in container.Elements())
            {
                element.AddBeforeSelf(new XText(childIndent));
                IndentChildrenElements(element, childIndent + oneIndentLevel, oneIndentLevel);
            }

            if (container.Elements().Any())
                container.Add(new XText(containerIndent));
        }

        private static void AddLeadingIndentation(XContainer container, string containerIndent, string oneIndentLevel)
        {
            bool containerIsSelfClosed = !container.Nodes().Any();
            XText lastChildText = container.LastNode as XText;
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
