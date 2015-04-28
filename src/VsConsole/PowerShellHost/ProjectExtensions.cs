using System.Management.Automation;
using EnvDTE;
using NuGet.PackageManagement.VisualStudio;
using NuGet.PackageManagement;
using System;
using Microsoft.VisualStudio.Shell;

namespace NuGetConsole.Host.PowerShell.Implementation
{
    public static class ProjectExtensions
    {
        /// <summary>
        /// This method is used for the ProjectName CodeProperty in Types.ps1xml
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "ps")]
        public static string GetCustomUniqueName(PSObject psObject)
        {
            return ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                return EnvDTEProjectUtility.GetCustomUniqueName((EnvDTE.Project)psObject.BaseObject);
            });
        }

        /// <summary>
        /// DO NOT delete this. This method is only called from PowerShell functional test. 
        /// </summary>
        public static void RemoveProject(string projectName)
        {
            if (String.IsNullOrEmpty(projectName))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, "projectName");
            }

            var solutionManager = (VSSolutionManager)ServiceLocator.GetInstance<ISolutionManager>();
            if (solutionManager != null)
            {
                var project = solutionManager.GetDTEProject(projectName);
                if (project == null)
                {
                    throw new InvalidOperationException();
                }

                var dte = ServiceLocator.GetInstance<DTE>();
                dte.Solution.Remove(project);
            }
        }
    }
}