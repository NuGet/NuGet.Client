using NuGet.Versioning;

namespace NuGet.ProjectModel.Test.Builders
{
    internal class LockFileDependencyBuilder
    {
        private string _id = "PackageA";
        private VersionRange _requestedVersion = new VersionRange(new NuGetVersion(1, 0, 0));
        private NuGetVersion _resolvedVersion = new NuGetVersion(1, 0, 0);
        private PackageDependencyType _type = PackageDependencyType.Direct;
        private string _contentHash = "ABC";

        public LockFileDependencyBuilder WithId(string id)
        {
            _id = id;
            return this;
        }

        public LockFileDependencyBuilder WithRequestedVersion(VersionRange requestedVersion)
        {
            _requestedVersion = requestedVersion;
            return this;
        }

        public LockFileDependencyBuilder WithResolvedVersion(NuGetVersion resolvedVersion)
        {
            _resolvedVersion = resolvedVersion;
            return this;
        }

        public LockFileDependencyBuilder WithType(PackageDependencyType type)
        {
            _type = type;
            return this;
        }

        public LockFileDependencyBuilder WithContentHash(string contentHash)
        {
            _contentHash = contentHash;
            return this;
        }

        public LockFileDependency Build()
        {
            return new LockFileDependency()
            {
                Id = _id,
                RequestedVersion = _requestedVersion,
                ResolvedVersion = _resolvedVersion,
                Type = _type,
                ContentHash = _contentHash
            };
        }
    }
}
