// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Configuration;

namespace NuGet.CommandLine.XPlat
{
    internal interface IListPackageReportWriter
    {
        // Command runner calls
        void WriteAutoReferenceDescription();
        void WriteSourcesDescription();
        void WriteSource(PackageSource source);
        void WriteAssetsFileNotFound(string path);
        void WriteErrorReadingAssetsFile(string assetsPath);
        void WriteNoPackagesFound(string projectName);
        void WriteNotPRProject(string path);
        void WriteNoUpdatesForProject(string projectName);
        void WriteNoDeprecatedPackagesForProject(string projectName);
        void WriteNoVulnerablePackagesForProject(string projectName);
        void WriteProjectOrSolutionFileNotFound(string path);

        // Project packages print utility calls
        void WriteProjectHeaderLog(string projectName);
        void WriteProjectUpdatesHeaderLog(string projectName);
        void WriteProjectDeprecationsHeaderLog(string projectName);
        void WriteProjectVulnerabilitiesHeaderLog(string projectName);
        void WriteNoPackagesForFramework(string frameworkName);
        void WriteNoUpdatesForFramework(string frameworkName);
        void WriteNoDeprecationsForFramework(string frameworkName);
        void WriteNoVulnerabilitiesForFramework(string frameworkName);
        void WriteFrameworkName(string frameworkName);
        void WriteStringWithForegroundColor(ConsoleColor? foregroundColor, string value);
        void WriteEmptyLine();
    }
}
