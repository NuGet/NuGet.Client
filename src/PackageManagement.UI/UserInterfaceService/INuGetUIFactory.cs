using System.Collections.Generic;
using NuGet.ProjectManagement;

namespace NuGet.PackageManagement.UI
{
    public interface INuGetUIFactory
    {
        INuGetUI Create(INuGetUIContext uiContext, NuGetUIProjectContext uiProjectContext);
    }
}