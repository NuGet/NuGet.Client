// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using NuGet.CommandLine.XPlat.Utility;
using NuGet.Configuration;

namespace NuGet.CommandLine.XPlat
{
    internal class ListPackageConsoleWriter : IListPackageReportWriter
    {
        #region Command runner calls

        public void WriteAutoReferenceDescription() => Console.WriteLine(Strings.ListPkg_AutoReferenceDescription);

        public void WriteSourcesDescription()
        {
            Console.WriteLine();
            Console.WriteLine(Strings.ListPkg_SourcesUsedDescription);
        }

        public void WriteSource(PackageSource packageSource) => Console.WriteLine("   " + packageSource.Source);

        public void WriteAssetsFileNotFound(string path)
        {
            Console.Error.WriteLine(string.Format(CultureInfo.CurrentCulture, Strings.Error_AssetsFileNotFound, path));
            Console.WriteLine();
        }

        public void WriteErrorReadingAssetsFile(string assetsPath) =>
            Console.WriteLine(string.Format(Strings.ListPkg_ErrorReadingAssetsFile, assetsPath));

        public void WriteNoPackagesFound(string projectName) =>
            Console.WriteLine(string.Format(Strings.ListPkg_NoPackagesFoundForFrameworks, projectName));

        public void WriteNoUpdatesForProject(string projectName) =>
            Console.WriteLine(string.Format(Strings.ListPkg_NoUpdatesForProject, projectName));

        public void WriteNoDeprecatedPackagesForProject(string projectName) =>
            Console.WriteLine(string.Format(Strings.ListPkg_NoDeprecatedPackagesForProject, projectName));

        public void WriteNoVulnerablePackagesForProject(string projectName) =>
            Console.WriteLine(string.Format(Strings.ListPkg_NoVulnerablePackagesForProject, projectName));

        public void WriteNotPRProject(string path)
        {
            Console.Error.WriteLine(string.Format(CultureInfo.CurrentCulture, Strings.Error_NotPRProject, path));
            Console.WriteLine();
        }
        public void WriteProjectOrSolutionFileNotFound(string path) =>
            Console.Error.WriteLine(string.Format(CultureInfo.CurrentCulture, Strings.ListPkg_ErrorFileNotFound, path));

        #endregion

        #region Project packages print utility calls

        public void WriteProjectHeaderLog(string projectName) =>
            Console.WriteLine(string.Format(Strings.ListPkg_ProjectHeaderLog, projectName));

        public void WriteProjectUpdatesHeaderLog(string projectName) =>
            Console.WriteLine(string.Format(Strings.ListPkg_ProjectUpdatesHeaderLog, projectName));

        public void WriteProjectDeprecationsHeaderLog(string projectName) =>
            Console.WriteLine(string.Format(Strings.ListPkg_ProjectDeprecationsHeaderLog, projectName));

        public void WriteProjectVulnerabilitiesHeaderLog(string projectName) =>
            Console.WriteLine(string.Format(Strings.ListPkg_ProjectVulnerabilitiesHeaderLog, projectName));

        public void WriteNoPackagesForFramework(string frameworkName)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine(string.Format("   [{0}]: " + Strings.ListPkg_NoPackagesForFramework, frameworkName));
            Console.ResetColor();
        }

        public void WriteNoUpdatesForFramework(string frameworkName)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine(string.Format("   [{0}]: " + Strings.ListPkg_NoUpdatesForFramework, frameworkName));
            Console.ResetColor();
        }

        public void WriteNoDeprecationsForFramework(string frameworkName)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine(string.Format("   [{0}]: " + Strings.ListPkg_NoDeprecationsForFramework, frameworkName));
            Console.ResetColor();
        }

        public void WriteNoVulnerabilitiesForFramework(string frameworkName)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine(string.Format("   [{0}]: " + Strings.ListPkg_NoVulnerabilitiesForFramework, frameworkName));
            Console.ResetColor();
        }

        public void WriteFrameworkName(string frameworkName)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine(string.Format("   [{0}]: ", frameworkName));
            Console.ResetColor();
        }

        public void WriteStringWithForegroundColor(ConsoleColor? foregroundColor, string value)
        {
            if (foregroundColor.HasValue)
            {
                Console.ForegroundColor = foregroundColor.Value;
            }

            Console.Write(value);
            Console.ResetColor();
        }

        public void WriteEmptyLine() => Console.WriteLine();

        #endregion
    }
}
