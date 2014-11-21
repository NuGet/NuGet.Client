using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace NuGet.Packaging
{
    public class KeyValueTreeProperty : TreeProperty
    {
        private readonly string _key;
        private readonly string _value;

        public KeyValueTreeProperty(string key, string value, bool isDefault = false)
            : base(isDefault)
        {
            _key = key;
            _value = value;
        }

        public KeyValueTreeProperty(XElement xml)
            : base(xml)
        {

        }

        public override string PivotKey
        {
            get
            {
                return Key;
            }
        }

        public string Key
        {
            get
            {
                return _key;
            }
        }

        public string Value
        {
            get
            {
                return _value;
            }
        }

        public override bool Satisfies(TreeProperty other)
        {
            return Equals(this, other);
        }

        public override string ToNormalizedString()
        {
            return String.Format(CultureInfo.InvariantCulture, "{0}:{1}", _key, _value);
        }

        public override string ToJson()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("{");

            sb.AppendLine("\"key\": \"" + Key + "\",");
            sb.AppendLine("\"value\": \"" + Value + "\"");

            if (_isDefault)
            {
                sb.AppendLine(",");
                sb.AppendLine("\"default\": true");
            }

            sb.AppendLine("}");

            return sb.ToString();
        }

        public override XElement ToXml()
        {
            //XAttribute type = new XAttribute("type", PackagingConstants.Schema.TreePropertyTypes.KeyValueProperty);
            XAttribute isDefault = new XAttribute(XName.Get("default", PackagingConstants.PackageCoreNamespace), _isDefault ? true : false);

            XElement key = new XElement(XName.Get("key", PackagingConstants.NuGetPackageNamespace), Key);
            XElement value = new XElement(XName.Get("value", PackagingConstants.NuGetPackageNamespace), Value);

            //return new XElement(XName.Get("keyValuePair", PackagingConstants.NuGetPackageNamespace), type, isDefault, key, value);
            return new XElement(XName.Get("keyValuePair", PackagingConstants.NuGetPackageNamespace), isDefault, key, value);
        }
    }
}
