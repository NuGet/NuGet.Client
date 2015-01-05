using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Client.VisualStudio.Models
{
    public class VisualStudioUIPackageDependency
    {
         public string Id
        {
            get;
            private set;
        }

        public VersionRange Range
        {
            get;
            private set;
        }

        public VisualStudioUIPackageDependency(string id, VersionRange range)
        {
            Id = id;
            Range = range;
        }

        public override string ToString()
        {
            if (Range == null)
            {
                return Id;
            }
            else
            {
                return String.Format(
                    CultureInfo.InvariantCulture,
                    "{0} {1}",
                    Id, Range.PrettyPrint());
            }
        }
    }
}
