using NuGet.PackagingCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace NuGet.Packaging
{
    public class DevTreeItem : PackageItem
    {
        private readonly KeyValuePair<string, string>[] _data;
        private readonly string _type;

        public DevTreeItem(string type, bool required, IEnumerable<KeyValuePair<string, string>> data)
            : base(required)
        {
            _data = data.ToArray();
            _type = type;
        }

        /// <summary>
        /// Item type
        /// </summary>
        public string Type
        {
            get
            {
                return _type;
            }
        }

        /// <summary>
        /// Additional item attributes
        /// </summary>
        public IEnumerable<KeyValuePair<string, string>> Data
        {
            get
            {
                return _data;
            }
        }
    }
}
