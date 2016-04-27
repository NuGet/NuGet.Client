using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NuGet.Packaging;

namespace NuGet.CommandLine.Rules
{
    internal class MisplacedAssemblyRule : IPackageRule
    {
        public IEnumerable<PackageIssue> Validate(IPackage package)
        {
            foreach (IPackageFile file in package.GetFiles())
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

        private static PackageIssue CreatePackageIssueForAssembliesUnderLib(string target)
        {
            return new PackageIssue(
                AnalysisResources.AssemblyUnderLibTitle,
                String.Format(CultureInfo.CurrentCulture, AnalysisResources.AssemblyUnderLibDescription, target),
                AnalysisResources.AssemblyUnderLibSolution
            );
        }

        private static PackageIssue CreatePackageIssueForAssembliesOutsideLib(string target)
        {
            return new PackageIssue(
                AnalysisResources.AssemblyOutsideLibTitle,
                String.Format(CultureInfo.CurrentCulture, AnalysisResources.AssemblyOutsideLibDescription, target),
                AnalysisResources.AssemblyOutsideLibSolution
            );
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