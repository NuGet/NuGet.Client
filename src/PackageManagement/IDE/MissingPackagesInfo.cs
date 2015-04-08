using System;
using System.Collections.Generic;
using NuGet.Packaging;

namespace NuGet.PackageManagement
{
    public class MissingPackagesInfo
    {
        internal static readonly MissingPackagesInfo Empty = new MissingPackagesInfo(new Dictionary<PackageReference, IReadOnlyCollection<string>>(new PackageReferenceComparer()));
        internal Dictionary<PackageReference, IReadOnlyCollection<string>> InternalPackageReferences { get; }
        public IReadOnlyDictionary<PackageReference, IReadOnlyCollection<string>> PackageReferences
        {
            get
            {
                return InternalPackageReferences;
            }
        }

        public MissingPackagesInfo(Dictionary<PackageReference, IReadOnlyCollection<string>> packageReferences)
        {
            if(packageReferences == null)
            {
                throw new ArgumentNullException(nameof(packageReferences));
            }

            InternalPackageReferences = packageReferences;
        }
    }
}
