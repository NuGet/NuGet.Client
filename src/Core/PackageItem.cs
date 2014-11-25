using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace NuGet.PackagingCore
{
    /// <summary>
    /// The actual artifact, dependency, or deliverable in the tree.
    /// </summary>
    public abstract class PackageItem
    {
        private readonly bool _required;

        public PackageItem(bool required)
        {
            _required = required;
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
    }
}
