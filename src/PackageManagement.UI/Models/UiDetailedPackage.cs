using System;
using System.Collections.Generic;
using NuGet.Versioning;

namespace NuGet.PackageManagement.UI
{
    public class UiPackageMetadata
    {
        public NuGetVersion Version { get; set; }
        public string Summary { get; set; }

        public string Description { get; set; }

        public string Authors { get; set; }

        public string Owners { get; set; }

        public Uri IconUrl { get; set; }

        public Uri LicenseUrl { get; set; }

        public Uri ProjectUrl { get; set; }

        public string Tags { get; set; }

        public int DownloadCount { get; set; }

        public DateTimeOffset? Published { get; set; }

        public IEnumerable<UiPackageDependencySet> DependencySets { get; set; }

        // This property is used by data binding to display text "No dependencies"
        public bool HasDependencies { get; set; }
    }
}
