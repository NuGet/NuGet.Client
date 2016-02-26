using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NuGet.Frameworks;
using NuGet.Packaging.PackageCreation.Resources;

namespace NuGet.Packaging
{
    public class PackageReferenceSet
    {
        public PackageReferenceSet(IEnumerable<string> references)
            : this((NuGetFramework)null, references)
        {
        }

        public PackageReferenceSet(string targetFramework, IEnumerable<string> references)
            : this(targetFramework != null ? NuGetFramework.Parse(targetFramework) : null, references)
        {
        }

        public PackageReferenceSet(NuGetFramework targetFramework, IEnumerable<string> references)
        {
            if (references == null)
            {
                throw new ArgumentNullException(nameof(references));
            }

            TargetFramework = targetFramework;
            References = references.ToArray();
        }

        public IReadOnlyCollection<string> References { get; }

        public NuGetFramework TargetFramework { get; }

        public IEnumerable<string> Validate()
        {
            if (References.Any(reference => String.IsNullOrEmpty(reference)))
            {
                yield return String.Format(CultureInfo.CurrentCulture, NuGetResources.Manifest_RequiredMetadataMissing, "Reference");
            }
        }
    }
}