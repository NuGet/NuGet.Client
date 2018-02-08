// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NuGet.Common;

namespace NuGet.Packaging.Rules
{
    internal class MisplacedAssemblyRule : IPackageRule
    {
        public IEnumerable<PackageIssueLogMessage> Validate(PackageBuilder builder)
        {
            foreach (IPackageFile file in builder.Files)
            {
                string path = file.Path;
                string directory = Path.GetDirectoryName(path);

                // if under 'lib' directly
                if (directory.Equals(PackagingConstants.Folders.Lib, StringComparison.OrdinalIgnoreCase))
                {
                    if (PackageHelper.IsAssembly(path))
                    {
                        yield return CreatePackageIssueForAssembliesUnderLib(path);
                    }
                }
                else if (!ValidFolders.Any(folder => path.StartsWith(folder, StringComparison.OrdinalIgnoreCase)))
                {
                    // when checking for assemblies outside 'lib' folder, only check .dll files.
                    // .exe files are often legitimate outside 'lib'.
                    if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                        path.EndsWith(".winmd", StringComparison.OrdinalIgnoreCase))
                    {
                        yield return CreatePackageIssueForAssembliesOutsideLib(path);
                    }
                }
            }
        }

        private static PackageIssueLogMessage CreatePackageIssueForAssembliesUnderLib(string target)
        {
            return new PackageIssueLogMessage(
                String.Format(CultureInfo.CurrentCulture, AnalysisResources.AssemblyDirectlyUnderLibWarning, target),
                NuGetLogCode.NU5101,
                WarningLevel.Default,
                LogLevel.Warning);
        }

        private static PackageIssueLogMessage CreatePackageIssueForAssembliesOutsideLib(string target)
        {
            return new PackageIssueLogMessage(
                String.Format(CultureInfo.CurrentCulture, AnalysisResources.AssemblyOutsideLibWarning, target),
                NuGetLogCode.NU5100,
                WarningLevel.Default,
                LogLevel.Warning);
        }

        /// <summary>
        /// Folders that are expected to have .dll and .winmd files
        /// </summary>
        private static IEnumerable<string> ValidFolders
        {
            get
            {
                yield return PackagingConstants.Folders.Lib + Path.DirectorySeparatorChar;
                yield return PackagingConstants.Folders.Analyzers + Path.DirectorySeparatorChar;
                yield return PackagingConstants.Folders.Ref + Path.DirectorySeparatorChar;
                yield return PackagingConstants.Folders.Runtimes + Path.DirectorySeparatorChar;
                yield return PackagingConstants.Folders.Native + Path.DirectorySeparatorChar;
                yield return PackagingConstants.Folders.Build + Path.DirectorySeparatorChar;
                yield return PackagingConstants.Folders.Tools + Path.DirectorySeparatorChar;
                yield break;
            }
        }
    }
}