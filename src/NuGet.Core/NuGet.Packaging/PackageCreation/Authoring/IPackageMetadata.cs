using System;
using System.Collections.Generic;
using NuGet.Versioning;

namespace NuGet.Packaging
{
    public interface IPackageMetadata
    {
        string Id { get; }
        NuGetVersion Version { get; }

        string Title { get; }
        IEnumerable<string> Authors { get; }
        IEnumerable<string> Owners { get; }
        Uri IconUrl { get; }
        Uri LicenseUrl { get; }
        Uri ProjectUrl { get; }
        bool RequireLicenseAcceptance { get; }
        bool DevelopmentDependency { get; }
        string Description { get; }
        string Summary { get; }
        string ReleaseNotes { get; }
        string Language { get; }
        string Tags { get; }
        string Copyright { get; }

        /// <summary>
        /// Specifies assemblies from GAC that the package depends on.
        /// </summary>
        IEnumerable<FrameworkAssemblyReference> FrameworkAssemblies { get; }
        
        /// <summary>
        /// Returns sets of References specified in the manifest.
        /// </summary>
        ICollection<PackageReferenceSet> PackageAssemblyReferences { get; }

        /// <summary>
        /// Specifies sets other packages that the package depends on.
        /// </summary>
        IEnumerable<PackageDependencyGroup> DependencyGroups { get; }

        Version MinClientVersion { get; }

        /// <summary>
        /// Returns sets of Content Files specified in the manifest.
        /// </summary>
        ICollection<ManifestContentFiles> ContentFiles { get; }
    }
}