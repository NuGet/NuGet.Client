using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Internal.Utils;

namespace NuGet.Client
{
    public class PackageSource
    {
        public string Name { get; private set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1056:UriPropertiesShouldNotBeStrings")]
        public string Url { get; private set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1054:UriParametersShouldNotBeStrings", MessageId = "1#")]
        public PackageSource(string name, string url)
        {
            Name = name;
            Url = url;
        }

        public override bool Equals(object obj)
        {
            PackageSource other = obj as PackageSource;
            if (other == null)
            {
                return false;
            }

            return String.Equals(Name, other.Name, StringComparison.CurrentCultureIgnoreCase) &&
                String.Equals(Url, other.Url, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            return HashCodeCombiner.Start()
                .Add(Name)
                .Add(Url)
                .CombinedHash;
        }

        public override string ToString()
        {
            return Name + ": " + Url;
        }
    }
}
