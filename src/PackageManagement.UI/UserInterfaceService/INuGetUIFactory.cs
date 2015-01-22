using System.Collections.Generic;
using NuGet.ProjectManagement;

namespace NuGet.PackageManagement.UI
{
    public interface INuGetUIFactory
    {
        INuGetUI Create(IEnumerable<NuGetProject> projects, INuGetUIContext uiContext, NuGetUIProjectContext uiProjectContext);
    }
}