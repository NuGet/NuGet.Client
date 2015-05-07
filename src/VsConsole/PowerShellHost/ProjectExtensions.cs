// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Management.Automation;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using NuGet.PackageManagement;
using NuGet.PackageManagement.VisualStudio;

namespace NuGetConsole.Host.PowerShell.Implementation
{
    public static class ProjectExtensions
    {
        /// <summary>
        /// This method is used for the ProjectName CodeProperty in Types.ps1xml
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "ps")]
        public static string GetCustomUniqueName(PSObject psObject)
        {
            return ThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    return EnvDTEProjectUtility.GetCustomUniqueName((Project)psObject.BaseObject);
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
