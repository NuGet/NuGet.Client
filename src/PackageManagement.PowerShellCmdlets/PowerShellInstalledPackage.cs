// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using NuGet.ProjectManagement;
using NuGet.Versioning;
using Task = System.Threading.Tasks.Task;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    /// <summary>
    /// Represent the view of packages installed to project(s)
    /// </summary>
    public class PowerShellInstalledPackage : PowerShellPackage
    {
        public string ProjectName { get; set; }

        /// <summary>
        /// Get the view of installed packages. Use for Get-Package command.
        /// </summary>
        internal static List<PowerShellInstalledPackage> GetPowerShellPackageView(Dictionary<NuGetProject, IEnumerable<Packaging.PackageReference>> dictionary)
        {
            var views = new List<PowerShellInstalledPackage>();

            foreach (var entry in dictionary)
            {
                // entry.Value is an empty list if no packages are installed
                foreach (var package in entry.Value)
                {
                    var view = new PowerShellInstalledPackage()
                    {
                        Id = package.PackageIdentity.Id,
                        AsyncLazyVersions = new AsyncLazy<IEnumerable<NuGetVersion>>(() => Task.FromResult<IEnumerable<NuGetVersion>>(new[] { package.PackageIdentity.Version }), ThreadHelper.JoinableTaskFactory),
                        ProjectName = entry.Key.GetMetadata<string>(NuGetProjectMetadataKeys.Name),
                    };

                    views.Add(view);
                }
            }

            return views;
        }
    }
}
