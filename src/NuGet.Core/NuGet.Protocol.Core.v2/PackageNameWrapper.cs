using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using NuGet.Packaging.Core;

namespace NuGet.Protocol.Core.v2
{
    /// <summary>
    /// A simple wrapper used to pass a package identity in a legacy IPackageMetadata.
    /// </summary>
    internal class PackageNameWrapper : IPackageMetadata
    {
        private readonly PackageIdentity _identity;

        public PackageNameWrapper(PackageIdentity identity)
        {
            _identity = identity;
        }

        public SemanticVersion Version
        {
            get
            {
                return SemanticVersion.Parse(_identity.Version.ToString());
            }
        }

        public string Id
        {
            get
            {
                return _identity.Id;
            }
        }

        #region unused

        public IEnumerable<string> Authors
        {
            get
            {
                return new List<string>();
            }
        }

        public string Copyright
        {
            get
            {
                return string.Empty;
            }
        }

        public IEnumerable<PackageDependencySet> DependencySets
        {
            get
            {
                return new List<PackageDependencySet>();
            }
        }

        public string Description
        {
            get
            {
                return string.Empty;
            }
        }

        public bool DevelopmentDependency
        {
            get
            {
                return false;
            }
        }

        public IEnumerable<FrameworkAssemblyReference> FrameworkAssemblies
        {
            get
            {
                return new List<FrameworkAssemblyReference>();
            }
        }

        public Uri IconUrl
        {
            get
            {
                return null;
            }
        }

        public string Language
        {
            get
            {
                return "en-us";
            }
        }

        public Uri LicenseUrl
        {
            get
            {
                return null;
            }
        }

        public Version MinClientVersion
        {
            get
            {
                return new Version(2, 5, 0, 0);
            }
        }

        public IEnumerable<string> Owners
        {
            get
            {
                return new List<string>();
            }
        }

        public ICollection<PackageReferenceSet> PackageAssemblyReferences
        {
            get
            {
                return new Collection<PackageReferenceSet>();
            }
        }

        public Uri ProjectUrl
        {
            get
            {
                return null;
            }
        }

        public string ReleaseNotes
        {
            get
            {
                return string.Empty;
            }
        }

        public bool RequireLicenseAcceptance
        {
            get
            {
                return false;
            }
        }

        public string Summary
        {
            get
            {
                return string.Empty;
            }
        }

        public string Tags
        {
            get
            {
                return string.Empty;
            }
        }

        public string Title
        {
            get
            {
                return string.Empty;
            }
        }

        #endregion
    }
}
