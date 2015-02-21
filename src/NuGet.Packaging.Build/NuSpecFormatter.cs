using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace NuGet.Packaging.Build
{
    public class NuSpecFormatter
    {
        public MetadataBuilder Read(Stream stream)
        {
            return null;
        }

        public void Save(MetadataBuilder builder, Stream stream)
        {
            var metadata = new XElement("metadata");

            foreach (var pair in builder.GetValues())
            {
                if (HasValue(pair.Value))
                {
                    metadata.Add(new XElement(pair.Key, pair.Value));
                }
            }

            var sectionElements = new List<XElement>();

            foreach (var section in builder.GetSections())
            {
                var sectionEl = new XElement(section.Name);

                if (string.IsNullOrEmpty(section.GroupByProperty))
                {
                    foreach (var item in section.GetEntries())
                    {
                        var el = new XElement(section.ItemName);
                        foreach (var pair in item.GetValues())
                        {
                            el.Add(new XAttribute(pair.Key, pair.Value));
                        }
                        sectionEl.Add(el);
                    }
                }
                else
                {
                    foreach (var group in section.GetEntries().GroupBy(s => s.GetMetadataValue(section.GroupByProperty)))
                    {
                        var groupEl = new XElement("group", new XAttribute(section.GroupByProperty, group.Key));
                        foreach (var item in group)
                        {
                            var el = new XElement(section.ItemName);
                            foreach (var pair in item.GetValues())
                            {
                                if (string.Equals(pair.Key, section.GroupByProperty, StringComparison.OrdinalIgnoreCase))
                                {
                                    continue;
                                }

                                el.Add(new XAttribute(pair.Key, pair.Value?.ToString() ?? ""));
                            }
                            groupEl.Add(el);
                        }

                        sectionEl.Add(groupEl);
                    }
                }

                sectionElements.Add(sectionEl);
            }

            var document = new XDocument(
                new XElement("package",
                    metadata));

            sectionElements.ForEach(el => metadata.Add(el));

            document.Save(stream);
        }

        private bool HasValue(object value)
        {
            if (value == null)
            {
                return false;
            }

            var values = value as IEnumerable<object>;
            if (values != null && values.Any())
            {
                return false;
            }

            return value != null;
        }
    }
}