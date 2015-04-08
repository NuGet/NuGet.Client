using System.IO;
using NuGet.Versioning;

namespace NuGet.PackageManagement.VisualStudio
{
    public interface IScriptPackage
    {
        string Id { get; }

        string Version { get; }
    }
}
