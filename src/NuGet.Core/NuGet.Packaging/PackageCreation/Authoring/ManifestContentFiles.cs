using System.Xml.Serialization;

namespace NuGet
{
    [XmlType("files")]
    public class ManifestContentFiles
    {
        [XmlAttribute("include")]
        public string Include { get; set; }

        [XmlAttribute("exclude")]
        public string Exclude { get; set; }

        [XmlAttribute("buildAction")]
        public string BuildAction { get; set; }

        [XmlAttribute("copyToOutput")]
        public string CopyToOutput { get; set; }

        [XmlAttribute("flatten")]
        public string Flatten { get; set; }
    }
}