// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using Microsoft.Build.Framework;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.ProjectModel;
using ILogger = NuGet.Common.ILogger;
using Task = Microsoft.Build.Utilities.Task;

namespace NuGet.Build.Tasks
{
    /// <summary>
    /// Gets the project style.
    /// </summary>
    public sealed class GetRestoreProjectStyleTask : Task
    {
        /// <summary>
        /// Gets or sets a value indicating whether or not the project has any PackageReference items.
        /// </summary>
        [Required]
        public bool HasPackageReferenceItems { get; set; }

        /// <summary>
        /// Gets or sets the full path to the project directory.
        /// </summary>
        [Required]
        public string MSBuildProjectDirectory { get; set; }

        /// <summary>
        /// Gets or sets the name of the project file.
        /// </summary>
        [Required]
        public string MSBuildProjectName { get; set; }

        [Output]
        public bool PackageReferenceCompatibleProjectStyle { get; set; }

        /// <summary>
        /// The path to a project.json file.
        /// </summary>
        public string ProjectJsonPath { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="NuGet.ProjectModel.ProjectStyle"/> of the project.
        /// </summary>
        [Output]
        public ProjectStyle ProjectStyle { get; set; }

        /// <summary>
        /// Gets or sets the user specified project style of the project.
        /// </summary>
        public string RestoreProjectStyle { get; set; }

        public override bool Execute()
        {
            var log = new MSBuildLogger(Log);

            // Log Inputs
            BuildTasksUtility.LogInputParam(log, nameof(HasPackageReferenceItems), HasPackageReferenceItems.ToString());
            BuildTasksUtility.LogInputParam(log, nameof(MSBuildProjectDirectory), MSBuildProjectDirectory);
            BuildTasksUtility.LogInputParam(log, nameof(MSBuildProjectName), MSBuildProjectName);
            BuildTasksUtility.LogInputParam(log, nameof(ProjectJsonPath), ProjectJsonPath);
            BuildTasksUtility.LogInputParam(log, nameof(RestoreProjectStyle), RestoreProjectStyle);

            var result = MSBuildRestoreUtility.GetProjectRestoreStyle(RestoreProjectStyle, HasPackageReferenceItems, ProjectJsonPath, MSBuildProjectDirectory, MSBuildProjectName, log);

            PackageReferenceCompatibleProjectStyle = result.PackageReferenceCompatibleProjectStyle;
            ProjectStyle = result.ProjectStyle;

            // Log Outputs
            BuildTasksUtility.LogOutputParam(log, nameof(PackageReferenceCompatibleProjectStyle), PackageReferenceCompatibleProjectStyle.ToString());
            BuildTasksUtility.LogOutputParam(log, nameof(ProjectStyle), ProjectStyle.ToString());

            return !Log.HasLoggedErrors;
        }
    }
}
