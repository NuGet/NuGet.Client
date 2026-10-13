// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;
using NuGet.Configuration;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Versioning;
using NuGet.VisualStudio;
using NuGetConsole.Host.PowerShell;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    internal static class PackageInitScriptHelper
    {
        internal static IReadOnlyList<PowerShellPackageInitScript> GetScripts(
            IVsSolutionManager solutionManager,
            ISettings settings,
            NuGetPackageManager packageManager,
            CancellationToken token)
        {
            solutionManager.EnsureSolutionIsLoaded();
            return NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await TaskScheduler.Default;
                var enumerator = new InstalledPackageEnumerator(solutionManager, settings);
                var packages = await enumerator.EnumeratePackagesAsync(packageManager, token);
                return packages
                    .Select(package => new PowerShellPackageInitScript(package.Identity, package.InstallPath))
                    .Where(script => File.Exists(script.FullName))
                    .ToList();
            });
        }

        internal static IEnumerable<PowerShellPackageInitScript> GetMatchingScripts(
            IReadOnlyList<PowerShellPackageInitScript> scripts,
            string[]? packages,
            Action<ErrorRecord> writeError)
        {
            if (packages == null)
            {
                return scripts;
            }

            var selected = new HashSet<PowerShellPackageInitScript>();
            foreach (var package in packages)
            {
                var separator = package.LastIndexOf('@');
                var id = separator < 0 ? package : package.Substring(0, separator);
                NuGetVersion? version = null;
                if (separator >= 0 && !NuGetVersion.TryParse(package.Substring(separator + 1), out version))
                {
                    WriteInvalidPackageError(package, writeError);
                    continue;
                }

                var matches = scripts.Where(script =>
                    StringComparer.OrdinalIgnoreCase.Equals(script.Id, id)
                    && (version == null || script.Version == version)).ToList();

                if (matches.Count == 0)
                {
                    WriteInvalidPackageError(package, writeError);
                }
                else if (matches.Count > 1)
                {
                    writeError(new ErrorRecord(
                        new ArgumentException(string.Format(CultureInfo.CurrentCulture, Resources.InitScript_AmbiguousPackage, package)),
                        NuGetErrorId.AmbiguousPackageInitScript, ErrorCategory.InvalidArgument, package));
                }
                else
                {
                    selected.Add(matches[0]);
                }
            }

            return scripts.Where(selected.Contains);
        }

        private static void WriteInvalidPackageError(string package, Action<ErrorRecord> writeError)
        {
            writeError(new ErrorRecord(
                new ArgumentException(string.Format(CultureInfo.CurrentCulture, Resources.InitScript_PackageNotFound, package)),
                NuGetErrorId.PackageInitScriptNotFound, ErrorCategory.ObjectNotFound, package));
        }
    }
}
