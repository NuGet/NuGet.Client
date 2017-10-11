// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Frameworks;
using NuGet.ProjectManagement.Projects;

namespace NuGet.ProjectManagement
{
    /// <summary>
    /// Information used by <see cref="BuildIntegratedNuGetProject"/> when installing a package.
    /// </summary>
    public class BuildIntegratedInstallationContext
    {
        public BuildIntegratedInstallationContext(
            IEnumerable<NuGetFramework> successfulFrameworks,
            IEnumerable<NuGetFramework> unsucessfulFrameworks,
            IDictionary<NuGetFramework, string> originalFrameworks)
        {
            SuccessfulFrameworks = successfulFrameworks;
            UnsuccessfulFrameworks = unsucessfulFrameworks;
            OriginalFrameworks = originalFrameworks;
        }

        /// <summary>
        /// Shows the frameworks for which a preview restore operation was successful. Only use it
        /// in case of single package install case, and only for CpsPackageReference projects.
        /// </summary>
        public IEnumerable<NuGetFramework> SuccessfulFrameworks { get; }

        /// <summary>
        /// Shows the frameworks for which a preview restore operation was unsuccessful. Only use it
        /// in case of single package install case, and only for CpsPackageReference projects.
        /// </summary>
        public IEnumerable<NuGetFramework> UnsuccessfulFrameworks { get; }

        /// <summary>
        /// A mapping to allow the original framework string to fetched. This is important because MSBuild target
        /// framework evaluation depends on the target framework string matching exactly.
        /// </summary>
        public IDictionary<NuGetFramework, string> OriginalFrameworks { get; }
    }
}
