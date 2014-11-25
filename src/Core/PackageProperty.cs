using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace NuGet.PackagingCore
{
    public abstract class PackageProperty : IEqualityComparer<PackageProperty>
    {
        protected readonly bool _isDefault;
        protected readonly bool _isRootLevel;

        public PackageProperty()
            : this(false, false)
        {

        }

        public PackageProperty(bool isDefault, bool isRootLevel)
        {
            _isDefault = isDefault;
            _isRootLevel = isRootLevel;
        }

        public abstract bool Satisfies(PackageProperty other);

        public bool IsDefault
        {
            get
            {
                return _isDefault;
            }
        }

        public bool IsRootLevel
        {
            get
            {
                return _isRootLevel;
            }
        }

        public abstract string ToNormalizedString();

        /// <summary>
        /// A unique string that identifies the pivot point for the tree.
        /// </summary>
        public abstract string PivotKey { get; }

        public virtual bool Equals(PackageProperty x, PackageProperty y)
        {
            return StringComparer.Ordinal.Equals(x.ToNormalizedString(), y.ToNormalizedString());
        }

        public virtual int GetHashCode(PackageProperty obj)
        {
            return ToNormalizedString().GetHashCode();
        }

        public override string ToString()
        {
            return ToNormalizedString();
        }
    }
}
