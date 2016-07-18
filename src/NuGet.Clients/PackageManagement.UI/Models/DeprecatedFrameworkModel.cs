// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NuGet.Frameworks;
using NuGet.ProjectManagement;

namespace NuGet.PackageManagement.UI
{
    public class DeprecatedFrameworkModel
    {
        public DeprecatedFrameworkModel(NuGetFramework deprecated, string migrationUrl, IEnumerable<NuGetProject> projects)
        {
            Details = string.Format(
                CultureInfo.CurrentCulture,
                Resources.Text_DeprecatedFramework_DocumentLink_Before,
                deprecated.DotNetFrameworkName,
                deprecated.GetShortFolderName());

            MigrationUrl = migrationUrl;

            Projects = projects
                .Select(project => NuGetProject.GetUniqueNameOrName(project))
                .OrderBy(name => name)
                .ToList();
        }

        public string Details { get; }
        public string MigrationUrl { get; }
        public IReadOnlyList<string> Projects { get; }
    }
}
