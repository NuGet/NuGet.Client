// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using NuGet.ProjectManagement;
using NuGet.Protocol.VisualStudio;
using NuGet.Versioning;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    /// <summary>
    /// Represent package updates found from the remote package source
    /// </summary>
    public class PowerShellUpdatePackage : PowerShellPackage
    {
        public string Description { get; set; }

        public string ProjectName { get; set; }

        /// <summary>
        /// Get the view of PowerShellPackage. Used for Get-Package -Updates command.
        /// </summary>
        internal static PowerShellUpdatePackage GetPowerShellPackageUpdateView(PSSearchMetadata data, NuGetVersion version, VersionType versionType, NuGetProject project)
        {
            var package = new PowerShellUpdatePackage()
            {
                Id = data.Identity.Id,
                Description = data.Summary,
                ProjectName = project.GetMetadata<string>(NuGetProjectMetadataKeys.Name),
                AsyncLazyVersions = new AsyncLazy<IEnumerable<NuGetVersion>>(async delegate
                {
                    var results = (await data.Versions.Value) ?? Enumerable.Empty<NuGetVersion>();
                    results = results.OrderByDescending(v => v)
                                    .Where(r => r > version)
                                    .ToArray();

                    return results;
                }, ThreadHelper.JoinableTaskFactory)
            };

            switch (versionType)
            {
                case VersionType.Updates:
                    package.AllVersions = true;
                    break;

                case VersionType.Latest:
                    package.AllVersions = false;
                    break;

                default:
                    Debug.Fail("Unexpected version type passed.");
                    break;
            }

            return package;
        }
    }
}
