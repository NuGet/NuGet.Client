using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace NuGet.Packaging
{
    public abstract class TreeProperty : IEqualityComparer<TreeProperty>
    {
        protected readonly bool _isDefault;

        public TreeProperty(bool isDefault)
        {
            _isDefault = isDefault;
        }

        public TreeProperty(XElement xml)
        {

        }

        public abstract bool Satisfies(TreeProperty other);

        public bool IsDefault
        {
            get
            {
                return _isDefault;
            }
        }

        public abstract string ToNormalizedString();

        /// <summary>
        /// A unique string that identifies the pivot point for the tree.
        /// </summary>
        public abstract string PivotKey { get; }

        public virtual bool Equals(TreeProperty x, TreeProperty y)
        {
            return StringComparer.Ordinal.Equals(x.ToNormalizedString(), y.ToNormalizedString());
        }

        public virtual int GetHashCode(TreeProperty obj)
        {
            return ToNormalizedString().GetHashCode();
        }

        public override string ToString()
        {
            return ToNormalizedString();
        }

        public abstract string ToJson();

        public abstract XElement ToXml();
    }
}
