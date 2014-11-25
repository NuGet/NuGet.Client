using NuGet.PackagingCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace NuGet.Packaging
{
    public class KeyValueTreeProperty : PackageProperty
    {
        private readonly string _key;
        private readonly string _value;

        public KeyValueTreeProperty(string key, string value, bool isDefault = false, bool isRootLevel = false)
            : base(isDefault, isRootLevel)
        {
            _key = key;
            _value = value;
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

        public override bool Satisfies(PackageProperty other)
        {
            return Equals(this, other);
        }

        public override string ToNormalizedString()
        {
            return String.Format(CultureInfo.InvariantCulture, "{0}:{1}", _key, _value);
        }
    }
}
