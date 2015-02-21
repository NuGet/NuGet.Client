using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NuGet.Packaging.Build
{
    public class JsonFormatter
    {
        public bool SkipNullValues { get; set; } = true;

        public MetadataBuilder Read(Stream stream)
        {
            return null;
        }

        public void Write(MetadataBuilder builder, Stream stream)
        {
            var document = new JObject();

            foreach (var pair in builder.GetValues())
            {
                SetValue(document, pair.Key, pair.Value);
            }

            foreach (var section in builder.GetSections())
            {
                var sectionEl = new JObject();
                document[section.Name] = sectionEl;

                if (string.IsNullOrEmpty(section.GroupByProperty))
                {
                    foreach (var item in section.GetEntries())
                    {
                        var el = new JObject();
                        sectionEl[section.ItemName] = el;
                        foreach (var pair in item.GetValues())
                        {
                            SetValue(el, pair.Key, pair.Value);
                        }
                    }
                }
                else
                {
                    foreach (var group in section.GetEntries().GroupBy(s => s.GetMetadataValue(section.GroupByProperty)))
                    {
                        var groupEl = new JObject();
                        sectionEl[group.Key] = groupEl;

                        foreach (var item in group)
                        {
                            var el = new JObject();
                            groupEl[section.ItemName] = el;
                            foreach (var pair in item.GetValues())
                            {
                                if (string.Equals(pair.Key, section.GroupByProperty, StringComparison.OrdinalIgnoreCase))
                                {
                                    continue;
                                }

                                SetValue(el, pair.Key, pair.Value);
                            }
                        }
                    }
                }
            }

            var sw = new StreamWriter(stream) { AutoFlush = true };
            sw.Write(document.ToString(Formatting.Indented));
        }

        private void SetValue(JObject obj, string name, object value)
        {
            if (value == null && SkipNullValues)
            {
                return;
            }

            obj[name] = FromObject(value);
        }

        private JToken FromObject(object value)
        {
            return value == null ? null : JToken.FromObject(value);
        }
    }
}