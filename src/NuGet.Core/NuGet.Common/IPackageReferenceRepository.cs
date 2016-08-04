using System.Runtime.Versioning;

namespace NuGet.Common
{
    public interface IPackageReferenceRepository : IPackageRepository
    {
        void AddPackage(string packageId, SemanticVersion version, bool developmentDependency, FrameworkName targetFramework);
        FrameworkName GetPackageTargetFramework(string packageId);
    }
}
