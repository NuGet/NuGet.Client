using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    public static class ProjectExtensions
    {
        // Project type guids
        internal const string WebApplicationProjectTypeGuid = "{349C5851-65DF-11DA-9384-00065B846F21}";
        internal const string WebSiteProjectTypeGuid = "{E24C65DC-7377-472B-9ABA-BC803B73C61A}";
        internal const string CsharpProjectTypeGuid = "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}";
        internal const string VbProjectTypeGuid = "{F184B08F-C81C-45F6-A57F-5ABD9991F28F}";
        internal const string CppProjectTypeGuid = "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}";
        internal const string FsharpProjectTypeGuid = "{F2A71F9B-5D33-465A-A702-920D77279786}";
        internal const string JsProjectTypeGuid = "{262852C6-CD72-467D-83FE-5EEB1973A190}";
        internal const string CpsProjectTypeGuid = "{13B669BE-BB05-4DDF-9536-439F39A36129}";
        internal const string WixProjectTypeGuid = "{930C7802-8A8C-48F9-8165-68863BCCD9DD}";
        internal const string LightSwitchProjectTypeGuid = "{ECD6D718-D1CF-4119-97F3-97C25A0DFBF9}";
        internal const string NemerleProjectTypeGuid = "{edcc3b85-0bad-11db-bc1a-00112fde8b61}";
        internal const string InstallShieldLimitedEditionTypeGuid = "{FBB4BD86-BF63-432a-A6FB-6CF3A1288F83}";
        internal const string WindowsStoreProjectTypeGuid = "{BC8A1FFA-BEE3-4634-8014-F334798102B3}";
        internal const string SynergexProjectTypeGuid = "{BBD0F5D1-1CC4-42fd-BA4C-A96779C64378}";
        internal const string NomadForVisualStudioProjectTypeGuid = "{4B160523-D178-4405-B438-79FB67C8D499}";
        internal const string TDSProjectTypeGuid = "{CAA73BB0-EF22-4d79-A57E-DF67B3BA9C80}";
        internal const string TDSItemTypeGuid = "{6877B9B0-CDF7-4ff2-BC09-9608387B37F2}";
        internal const string DxJsProjectTypeGuid = "{1B19158F-E398-40A6-8E3B-350508E125F1}";
        internal const string DeploymentProjectTypeGuid = "{151d2e53-a2c4-4d7d-83fe-d05416ebd58e}";
        // All unloaded projects have this Kind value
        internal const string UnloadedProjectTypeGuid = "{67294A52-A4F0-11D2-AA88-00C04F688DDE}";

        // HResults
        internal const int S_OK = 0;
        internal const uint root = 4294967294;

        private static readonly HashSet<string> _unsupportedProjectTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                                                                            LightSwitchProjectTypeGuid,
                                                                            InstallShieldLimitedEditionTypeGuid
                                                                        };

        private static readonly HashSet<string> _supportedProjectTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
        {
            WebSiteProjectTypeGuid, 
            CsharpProjectTypeGuid, 
            VbProjectTypeGuid,
            CppProjectTypeGuid,
            JsProjectTypeGuid,
            CpsProjectTypeGuid,
            FsharpProjectTypeGuid,
            NemerleProjectTypeGuid,
            WixProjectTypeGuid,
            SynergexProjectTypeGuid,
            NomadForVisualStudioProjectTypeGuid,
            TDSProjectTypeGuid,
            DxJsProjectTypeGuid,
            DeploymentProjectTypeGuid
        };

        public static bool IsExplicitlyUnsupported(this Project project)
        {
            return project.Kind == null || _unsupportedProjectTypes.Contains(project.Kind);
        }

        public static bool IsSupported(this Project project)
        {
            Debug.Assert(project != null);

            if (project.SupportsINuGetProjectSystem())
            {
                return true;
            }

            return project.Kind != null && _supportedProjectTypes.Contains(project.Kind) && !project.IsSharedProject();
        }

        /// <summary>
        /// Check if the project has the SharedAssetsProject capability. This is true
        /// for shared projects in universal apps.
        /// </summary>
        public static bool IsSharedProject(this Project project)
        {
            var hier = project.ToVsHierarchy();

            return hier.IsCapabilityMatch("SharedAssetsProject");
        }

        public static IVsHierarchy ToVsHierarchy(this Project project)
        {
            IVsHierarchy hierarchy;

            // Get the vs solution
            IVsSolution solution = (IVsSolution)ServiceProvider.GlobalProvider.GetService(typeof(IVsSolution));
            int hr = solution.GetProjectOfUniqueName(project.GetUniqueName(), out hierarchy);

            if (hr != S_OK)
            {
                Marshal.ThrowExceptionForHR(hr);
            }

            return hierarchy;
        }

        public static string GetUniqueName(this Project project)
        {
            if (project.IsWixProject())
            {
                // Wix project doesn't offer UniqueName property
                return project.FullName;
            }

            try
            {
                return project.UniqueName;
            }
            catch (COMException)
            {
                return project.FullName;
            }
        }

        public static string GetName(this Project project)
        {
            string name = project.Name;
            if (project.IsJavaScriptProject())
            {
                // The JavaScript project initially returns a "(loading..)" suffix to the project Name.
                // Need to get rid of it for the rest of NuGet to work properly.
                // TODO: Follow up with the VS team to see if this will be fixed eventually
                const string suffix = " (loading...)";
                if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    name = name.Substring(0, name.Length - suffix.Length);
                }
            }
            return name;
        }

        /// <summary>
        /// Returns the unique name of the specified project including all solution folder names containing it.
        /// </summary>
        /// <remarks>
        /// This is different from the DTE Project.UniqueName property, which is the absolute path to the project file.
        /// </remarks>
        public static string GetCustomUniqueName(this Project project)
        {
            if (project.IsWebSite())
            {
                // website projects always have unique name
                return project.Name;
            }
            else
            {
                Stack<string> nameParts = new Stack<string>();

                Project cursor = project;
                nameParts.Push(cursor.GetName());

                // walk up till the solution root
                while (cursor.ParentProjectItem != null && cursor.ParentProjectItem.ContainingProject != null)
                {
                    cursor = cursor.ParentProjectItem.ContainingProject;
                    nameParts.Push(cursor.GetName());
                }

                return String.Join("\\", nameParts);
            }
        }

        public static string GetProjectSafeName(this Project project, DTE dte)
        {
            if (project == null)
            {
                throw new ArgumentNullException("project");
            }

            // Try searching for simple names first
            string name = project.GetName();
            Project target = dte.Solution.GetAllProjects().Where(p => StringComparer.OrdinalIgnoreCase.Equals(p.Name, name)).FirstOrDefault();
            if (target == project)
            {
                return name;
            }

            return project.GetCustomUniqueName();
        }

        public static bool IsWebSite(this Project project)
        {
            return project.Kind != null && project.Kind.Equals(WebSiteProjectTypeGuid, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsWixProject(this Project project)
        {
            return project.Kind != null && project.Kind.Equals(WixProjectTypeGuid, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsJavaScriptProject(this Project project)
        {
            return project != null && JsProjectTypeGuid.Equals(project.Kind, StringComparison.OrdinalIgnoreCase);
        }

        public static bool SupportsINuGetProjectSystem(this Project project)
        {
#if VS14
            return project.ToNuGetProjectSystem() != null;
#else
            return false;
#endif
        }
    }
}
