using System;

namespace NuGet.PackageManagement.VisualStudio
{
    public interface IVsSourceControlTracker
    {
        event EventHandler SolutionBoundToSourceControl;
    }
}
