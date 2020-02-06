using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using Microsoft.TeamFoundation.WorkItemTracking.Process.WebApi.Models;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.ProjectManagement;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.UI.TestContract
{
    [Export(typeof(NuGetApexUITestService))]
    public class NuGetApexUITestService
    {
        public NuGetApexUITestService()
        {
        }

        public ApexTestUIProject GetApexTestUIProject(string project, TimeSpan timeout, TimeSpan interval)
        {
            PackageManagerControl packageManagerControl = null;

            var timer = Stopwatch.StartNew();

            while (packageManagerControl == null && timer.Elapsed < timeout)
            {
                packageManagerControl = GetProjectPackageManagerControl(project);

                if (packageManagerControl == null)
                {
                    System.Threading.Thread.Sleep(interval);
                }
            }

            if (packageManagerControl == null)
            {
                throw new TimeoutException($"The package manager control did not load within {timeout}");
            }

            return new ApexTestUIProject(packageManagerControl);
        }

        private PackageManagerControl GetProjectPackageManagerControl(string projectUniqueName)
        {
            return NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var uiShell = ServiceLocator.GetGlobalService<SVsUIShell, IVsUIShell>();
                foreach (var windowFrame in VsUtility.GetDocumentWindows(uiShell))
                {
                    object docView;
                    var hr = windowFrame.GetProperty(
                        (int)__VSFPROPID.VSFPROPID_DocView,
                        out docView);
                    if (hr == VSConstants.S_OK
                        && docView is PackageManagerWindowPane)
                    {
                        var packageManagerWindowPane = (PackageManagerWindowPane)docView;
                        if (packageManagerWindowPane.Model.IsSolution)
                        {
                            // the window is the solution package manager
                            continue;
                        }

                        var projects = packageManagerWindowPane.Model.Context.Projects;
                        if (projects.Count() != 1)
                        {
                            continue;
                        }

                        var existingProject = projects.First();
                        var projectName = existingProject.GetMetadata<string>(NuGetProjectMetadataKeys.Name);
                        if (string.Equals(projectName, projectUniqueName, StringComparison.OrdinalIgnoreCase))
                        {
                            var packageManagerControl = VsUtility.GetPackageManagerControl(windowFrame);
                            if (packageManagerControl != null)
                            {
                                return packageManagerControl;
                            }
                        }
                    }
                }

                return null;
            });
        }
    }
}
