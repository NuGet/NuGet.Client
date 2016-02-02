
using NuGet.Versioning;

namespace NuGet.Packaging
{
    public interface IPackageName
    {
        string Id { get; }
        SemanticVersion Version { get; }
    }
}
