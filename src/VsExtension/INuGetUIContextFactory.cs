using System.Collections.Generic;
using NuGet.PackageManagement.UI;
using NuGet.ProjectManagement;

namespace NuGetVSExtension
{
    public interface INuGetUIContextFactory
    {
        INuGetUIContext Create(NuGetPackage package, IEnumerable<NuGetProject> projects);
    }
}