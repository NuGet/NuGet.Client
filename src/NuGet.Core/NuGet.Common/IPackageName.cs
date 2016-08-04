
namespace NuGet.Common
{
    public interface IPackageName
    {
        string Id { get; }
        SemanticVersion Version { get; }
    }
}
