using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
#if !VS14
using Microsoft.VisualStudio.Shell;
#endif
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.VisualStudio
{
    public static class DeferredProjectVSUtility
    {
#if !VS14
        private static Lazy<IVsSolution> _vsSolution = new Lazy<IVsSolution>(() => GetVsSolution());
#endif

        public static async Task<bool> SolutionHasDeferredProjectsAsync()
        {
#if VS14
            // for Dev14 always return false since DPL not exists there.
            return await Task.FromResult(false);
#else
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // check if solution is DPL enabled or not. 
            if (!IsSolutionDPLEnabled())
            {
                return false;
            }

            // Get deferred projects count of current solution
            var value = GetVSSolutionProperty((int)(__VSPROPID7.VSPROPID_DeferredProjectCount));
            return (int)value != 0;
#endif
        }

        public static bool IsSolutionDPLEnabled()
        {
#if VS14
            // for Dev14 always return false since DPL not exists there.
            return false;
#else
            return NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var vsSolution7 = _vsSolution.Value as IVsSolution7;

                if (vsSolution7 != null && vsSolution7.IsSolutionLoadDeferred())
                {
                    return true;
                }

                return false;
            });
#endif
        }

        public static async Task<IEnumerable<string>> GetDeferredProjectsFilePathAsync()
        {
#if VS14
            // Not applicable for Dev14 so always return empty list.
            return await Task.FromResult(Enumerable.Empty<string>());
#else
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var projectPaths = new List<string>();
            IEnumHierarchies enumHierarchies;
            var guid = Guid.Empty;
            var hr = _vsSolution.Value.GetProjectEnum((uint)__VSENUMPROJFLAGS3.EPF_DEFERRED, ref guid, out enumHierarchies);

            ErrorHandler.ThrowOnFailure(hr);

            // Loop all projects found
            if (enumHierarchies != null)
            {
                // Loop projects found
                var hierarchy = new IVsHierarchy[1];
                uint fetched = 0;
                while (enumHierarchies.Next(1, hierarchy, out fetched) == VSConstants.S_OK && fetched == 1)
                {
                    string projectPath;
                    hierarchy[0].GetCanonicalName(VSConstants.VSITEMID_ROOT, out projectPath);

                    if (!string.IsNullOrEmpty(projectPath))
                    {
                        projectPaths.Add(projectPath);
                    }
                }
            }

            return projectPaths;
#endif
        }

#if !VS14

        private static object GetVSSolutionProperty(int propId)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            object value;
            var hr = _vsSolution.Value.GetProperty(propId, out value);

            ErrorHandler.ThrowOnFailure(hr);

            return value;
        }

        private static IVsSolution GetVsSolution()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var serviceProvider = ServiceLocator.GetInstance<IServiceProvider>();

            return serviceProvider.GetService<SVsSolution, IVsSolution>();
        }
#endif
    }
}
