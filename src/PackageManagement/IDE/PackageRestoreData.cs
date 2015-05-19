using System;
using System.Collections.Generic;
using NuGet.Packaging;

namespace NuGet.PackageManagement
{
    public class PackageRestoreData
    {
        public PackageReference PackageReference { get; }
        public IEnumerable<string> ProjectNames { get; }
        public bool IsMissing { get; }

        public PackageRestoreData(PackageReference packageReference, IEnumerable<string> projectNames, bool isMissing)
        {
            if (packageReference == null)
            {
                throw new ArgumentNullException(nameof(packageReference));
            }

            if (projectNames == null)
            {
                throw new ArgumentNullException(nameof(projectNames));
            }

            PackageReference = packageReference;
            ProjectNames = projectNames;
            IsMissing = isMissing;
        }
    }
}
