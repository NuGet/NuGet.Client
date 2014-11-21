using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace NuGet.Packaging
{
    /// <summary>
    /// The actual artifact, dependency, or deliverable in the tree.
    /// </summary>
    public abstract class TreeItem
    {
        private readonly bool _required;

        public TreeItem(bool required)
        {
            _required = required;
        }

        public TreeItem(XElement xml)
        {
            var att = xml.Attribute(XName.Get("required", PackagingConstants.PackageCoreNamespace));

            if (att != null && att.Value == "true")
            {
                _required = true;
            }
        }

        /// <summary>
        /// True if the item is required. The build should fail if a required type cannot be understood.
        /// </summary>
        public bool Required
        {
            get
            {
                return _required;
            }
        }

        public abstract XElement ToXml();
    }
}
