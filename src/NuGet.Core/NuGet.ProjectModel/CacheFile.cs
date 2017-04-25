using System;
using System.Collections.Generic;
using System.Text;
using NuGet.Shared;

namespace NuGet.ProjectModel
{
    public class CacheFile : IEquatable<CacheFile>
    {
        public string DgSpecHash { get; set; }

        public bool IsValid { get { return DgSpecHash==null ? DgSpecHash.Length == 128 : false; } }


        public bool Equals(CacheFile other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return StringComparer.Ordinal.Equals(DgSpecHash, other.DgSpecHash);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as CacheFile);
        }

        public override int GetHashCode()
        {
            var combiner = new HashCodeCombiner();
            combiner.AddObject(DgSpecHash);
            return combiner.CombinedHash;
        }

    }

}
