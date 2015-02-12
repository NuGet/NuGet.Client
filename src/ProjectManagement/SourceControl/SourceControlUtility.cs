using NuGet.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.ProjectManagement
{
    public static class SourceControlUtility
    {
        private const string SolutionSection = "solution";
        private const string DisableSourceControlIntegerationKey = "disableSourceControlIntegration";

        public static bool IsSourceControlDisabled(ISettings settings)
        {
            var value = settings.GetValue(SolutionSection, DisableSourceControlIntegerationKey);
            bool disableSourceControlIntegration;
            return !String.IsNullOrEmpty(value) && Boolean.TryParse(value, out disableSourceControlIntegration) && disableSourceControlIntegration;
        }

        public static void DisableSourceControlMode(ISettings settings)
        {
            settings.SetValue(SolutionSection, DisableSourceControlIntegerationKey, "true");
        }

        public static SourceControlManager GetSourceControlManager(INuGetProjectContext nuGetProjectContext)
        {
            if (nuGetProjectContext != null)
            {
                var sourceControlManagerProvider = nuGetProjectContext.SourceControlManagerProvider;
                if (sourceControlManagerProvider != null)
                {
                    return sourceControlManagerProvider.GetSourceControlManager();
                }
            }

            return null;
        }

        public static bool IsPackagesFolderBoundToSourceControl(INuGetProjectContext nuGetProjectContext)
        {
            var sourceControlManager = GetSourceControlManager(nuGetProjectContext);
            if (sourceControlManager != null)
            {
                return sourceControlManager.IsPackagesFolderBoundToSourceControl();
            }

            return false;
        }
    }
}
