// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Management.Automation;
using NuGet.PackageManagement.VisualStudio;
using NuGet.VisualStudio;

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
            return NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    return await ((EnvDTE.Project)psObject.BaseObject).GetCustomUniqueNameAsync();
                });
        }

        /// <summary>
        /// DO NOT delete this. This method is only called from PowerShell functional test.
        /// </summary>
        public static void RemoveProject(string projectName)
        {
            if (string.IsNullOrEmpty(projectName))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, "projectName");
            }

            NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var solutionManager = ServiceLocator.GetInstance<IVsSolutionManager>();
                if (solutionManager != null)
                {
                    var project = await solutionManager.GetVsProjectAdapterAsync(projectName);
                    if (project == null)
                    {
                        throw new InvalidOperationException();
                    }

                    var dte = ServiceLocator.GetInstance<EnvDTE.DTE>();
                    dte.Solution.Remove(project.Project);
                }
            });
        }
    }
}
