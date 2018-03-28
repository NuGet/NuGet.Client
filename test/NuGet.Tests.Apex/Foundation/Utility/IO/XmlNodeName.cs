using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Tests.Foundation.Utility.IO
{
    [Serializable]
    public struct XmlNodeName : IEquatable<XmlNodeName>
    {
        public string LocalName { get; set; }
        public string Namespace { get; set; }
        public string Attribute { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is XmlNodeName)
            {
                return this.Equals((XmlNodeName)obj);
            }

            return false;
        }

        public bool Equals(XmlNodeName other)
        {
            return String.Equals(this.LocalName, other.LocalName, StringComparison.Ordinal)
                && String.Equals(this.Namespace, other.Namespace, StringComparison.Ordinal)
                && String.Equals(this.Attribute, other.Attribute, StringComparison.Ordinal);
        }

        public override int GetHashCode()
        {
            string localName = this.LocalName;
            if (localName != null)
            {
                return localName.GetHashCode();
            }
            else
            {
                return base.GetHashCode();
            }
        }

        public static bool operator ==(XmlNodeName left, XmlNodeName right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(XmlNodeName left, XmlNodeName right)
        {
            return !(left == right);
        }
    }
}
