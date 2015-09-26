using System;
using System.Collections.Generic;
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
                throw new NotImplementedException();
            }
        }

        public string Copyright
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public IEnumerable<PackageDependencySet> DependencySets
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public string Description
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public bool DevelopmentDependency
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public IEnumerable<FrameworkAssemblyReference> FrameworkAssemblies
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public Uri IconUrl
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public string Language
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public Uri LicenseUrl
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public Version MinClientVersion
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public IEnumerable<string> Owners
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public ICollection<PackageReferenceSet> PackageAssemblyReferences
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public Uri ProjectUrl
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public string ReleaseNotes
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public bool RequireLicenseAcceptance
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public string Summary
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public string Tags
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public string Title
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        #endregion
    }
}
