using System;
using System.IO;

namespace NuGet.Common
{
    public interface IPackageCacheRepository : IPackageRepository
    {
        bool InvokeOnPackage(string packageId, SemanticVersion version, Action<Stream> action);
    }
}