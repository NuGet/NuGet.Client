using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using NuGet.Frameworks;

namespace NuGet.Test.Utility
{
    public class ProjectFileUtils
    {
        public static void AddProperties(XDocument doc, Dictionary<string, string> properties)
        {
            var ns = doc.Root.GetDefaultNamespace();

            var propertyGroup = new XElement(XName.Get("PropertyGroup", ns.NamespaceName));
            foreach (var pair in properties)
            {
                var subItem = new XElement(XName.Get(pair.Key, ns.NamespaceName), pair.Value);
                propertyGroup.Add(subItem);
            }

            var lastPropGroup = doc.Root.Elements().Last(e => e.Name.LocalName == "PropertyGroup");
            lastPropGroup.AddAfterSelf(propertyGroup);
        }

        public static void AddProperty(XDocument doc, string propertyName, string propertyValue)
        {
            var lastPropGroup = doc.Root.Elements().Last(e => e.Name.LocalName == "PropertyGroup");
            lastPropGroup.Add(new XElement(XName.Get(propertyName), propertyValue));
        }

        public static void AddItem(XDocument doc,
            string name,
            string identity,
            NuGetFramework framework,
            Dictionary<string, string> properties,
            Dictionary<string,string> attributes )
        {
            AddItem(doc, name, identity,
                framework?.IsSpecificFramework == true ? framework.GetShortFolderName() : string.Empty, properties, attributes);
        }

        public static void AddItem(XDocument doc,
            string name,
            string identity,
            string framework,
            Dictionary<string, string> properties,
            Dictionary<string,string> attributes )
        {
            var ns = doc.Root.GetDefaultNamespace();

            var itemGroup = new XElement(XName.Get("ItemGroup", ns.NamespaceName));
            var entry = new XElement(XName.Get(name, ns.NamespaceName));
            entry.Add(new XAttribute(XName.Get("Include"), identity));
            itemGroup.Add(entry);

            if (!string.IsNullOrEmpty(framework))
            {
                itemGroup.Add(new XAttribute(XName.Get("Condition"), $" '$(TargetFramework)' == '{framework}' "));
            }

            foreach (var attribute in attributes)
            {
                var attr = new XAttribute(XName.Get(attribute.Key), attribute.Value);
                entry.Add(attr);
            }

            foreach (var pair in properties)
            {
                var subItem = new XElement(XName.Get(pair.Key, ns.NamespaceName), pair.Value);
                entry.Add(subItem);
            }

            var lastItemGroup = doc.Root.Elements().LastOrDefault(e => e.Name.LocalName == "ItemGroup");
            if (lastItemGroup == null)
            {
                doc.Root.Elements().Last().AddAfterSelf(itemGroup);
            }
            else
            {
                lastItemGroup.AddAfterSelf(itemGroup);
            }
        }

        public static void SetTargetFrameworkForProject(XDocument doc, string targetFrameworkPropertyName, string targetFrameworkValue)
        {
            var existingFrameworkProperty = "TargetFramework";
            var pgElement = doc.Root.Descendants("PropertyGroup").FirstOrDefault(t => t.Descendants("TargetFramework").Any());
            if (pgElement == null)
            {
                pgElement = doc.Root.Descendants("PropertyGroup").FirstOrDefault(t => t.Descendants("TargetFrameworks").Any());
                existingFrameworkProperty = "TargetFrameworks";
            }

            if (pgElement != null)
            {
                pgElement.SetElementValue(XName.Get(existingFrameworkProperty), null);
                pgElement.SetElementValue(XName.Get(targetFrameworkPropertyName), targetFrameworkValue);
            }
            else
            {
                AddProperty(doc, targetFrameworkPropertyName, targetFrameworkValue);
            }
        }

        public static void WriteXmlToFile(XDocument xml, FileStream stream)
        {
            var unicodeEncoding = new UTF8Encoding();
            var xmlString = xml.ToString();
            stream.SetLength(0);
            stream.Seek(0, SeekOrigin.Begin);
            stream.Write(unicodeEncoding.GetBytes(xmlString), 0, unicodeEncoding.GetByteCount(xmlString));
        }
    }
}
