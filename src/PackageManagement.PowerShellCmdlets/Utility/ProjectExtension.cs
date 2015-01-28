using EnvDTE;
using System;
using System.Collections.Generic;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    public static class ProjectExtensions
    {
        internal const string LightSwitchProjectTypeGuid = "{ECD6D718-D1CF-4119-97F3-97C25A0DFBF9}";
        internal const string InstallShieldLimitedEditionTypeGuid = "{FBB4BD86-BF63-432a-A6FB-6CF3A1288F83}";

        private static readonly HashSet<string> _unsupportedProjectTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                                                                            LightSwitchProjectTypeGuid,
                                                                            InstallShieldLimitedEditionTypeGuid
                                                                        };

        public static bool IsExplicitlyUnsupported(this Project project)
        {
            return project.Kind == null || _unsupportedProjectTypes.Contains(project.Kind);
        }
    }
}
