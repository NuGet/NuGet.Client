// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Common;
using NuGet.Frameworks;

namespace NuGet.Build.Tasks
{
    public class GetReferenceNearestTargetFrameworkTask : Task
    {
        private const string NEAREST_TARGET_FRAMEWORK = "NearestTargetFramework";
        private const string TARGET_FRAMEWORKS = "TargetFrameworks";
        private const string MSBUILD_SOURCE_PROJECT_FILE = "MSBuildSourceProjectFile";

        /// <summary>
        /// The current project's name.
        /// </summary>
        public string CurrentProjectName { get; set; }

        /// <summary>
        /// The current project's target framework.
        /// </summary>
        [Required]
        public string CurrentProjectTargetFramework { get; set; }

        /// <summary>
        /// Optional list of target frameworks to be used as Fallback target frameworks.
        /// </summary>
        public string[] FallbackTargetFrameworks { get; set; }

        /// <summary>
        /// The project references for property lookup.
        /// </summary>
        public ITaskItem[] AnnotatedProjectReferences { get; set; }

        /// <summary>
        /// The project references with assigned properties.
        /// </summary>
        [Output]
        public ITaskItem[] AssignedProjects { get; set; }

        public override bool Execute()
        {

            var logger = new MSBuildLogger(Log);

            BuildTasksUtility.LogInputParam(logger, nameof(CurrentProjectTargetFramework), CurrentProjectTargetFramework);

            BuildTasksUtility.LogInputParam(logger, nameof(FallbackTargetFrameworks),
                FallbackTargetFrameworks == null
                    ? ""
                    : string.Join(";", FallbackTargetFrameworks.Select(p => p)));

            BuildTasksUtility.LogInputParam(logger, nameof(AnnotatedProjectReferences),
                AnnotatedProjectReferences == null
                    ? ""
                    : string.Join(";", AnnotatedProjectReferences.Select(p => p.ItemSpec)));

            if (AnnotatedProjectReferences == null)
            {
                return !Log.HasLoggedErrors;
            }

            var fallbackNuGetFrameworks = new List<NuGetFramework>();

            // validate current project framework
            var errorMessage = string.Format(Strings.UnsupportedTargetFramework, CurrentProjectTargetFramework);
            if (!TryParseFramework(CurrentProjectTargetFramework, errorMessage, logger, out var projectNuGetFramework))
            {
                return false;
            }

            if (FallbackTargetFrameworks != null &&
                FallbackTargetFrameworks.Length > 0)
            {
                foreach (var fallbackFramework in FallbackTargetFrameworks)
                {
                    // validate ATF project frameworks
                    errorMessage = string.Format(Strings.UnsupportedFallbackFramework, fallbackFramework);
                    if (!TryParseFramework(fallbackFramework, errorMessage, logger, out var nugetFramework))
                    {
                        return false;
                    }
                    else
                    {
                        fallbackNuGetFrameworks.Add(nugetFramework);
                    }
                }
            }

            AssignedProjects = new ITaskItem[AnnotatedProjectReferences.Length];
            for (var index = 0; index < AnnotatedProjectReferences.Length; index++)
            {
                AssignedProjects[index] = AssignNearestFrameworkForSingleReference(AnnotatedProjectReferences[index], projectNuGetFramework, fallbackNuGetFrameworks, logger);
            }

            BuildTasksUtility.LogOutputParam(logger, nameof(AssignedProjects), string.Join(";", AssignedProjects.Select(p => p.ItemSpec)));

            return !Log.HasLoggedErrors;
        }

        private ITaskItem AssignNearestFrameworkForSingleReference(
            ITaskItem project,
            NuGetFramework projectNuGetFramework,
            IList<NuGetFramework> fallbackNuGetFrameworks,
            MSBuildLogger logger)
        {
            var itemWithProperties = new TaskItem(project);
            var referencedProjectFrameworkString = project.GetMetadata(TARGET_FRAMEWORKS);
            var referencedProjectFile = project.GetMetadata(MSBUILD_SOURCE_PROJECT_FILE);

            if (string.IsNullOrEmpty(referencedProjectFrameworkString))
            {
                // No target frameworks set, nothing to do.
                return itemWithProperties;
            }

            var referencedProjectFrameworks = MSBuildStringUtility.Split(referencedProjectFrameworkString);

            // try project framework
            var nearestNuGetFramework = NuGetFrameworkUtility.GetNearest(referencedProjectFrameworks, projectNuGetFramework, NuGetFramework.Parse);
            if (nearestNuGetFramework != null)
            {
                itemWithProperties.SetMetadata(NEAREST_TARGET_FRAMEWORK, nearestNuGetFramework);
                return itemWithProperties;
            }

            // try project fallback frameworks
            foreach (var currentProjectTargetFramework in fallbackNuGetFrameworks)
            {
                nearestNuGetFramework = NuGetFrameworkUtility.GetNearest(referencedProjectFrameworks, currentProjectTargetFramework, NuGetFramework.Parse);

                if (nearestNuGetFramework != null)
                {
                    var message = string.Format(CultureInfo.CurrentCulture,
                        Strings.ImportsFallbackWarning,
                        referencedProjectFile,
                        currentProjectTargetFramework.DotNetFrameworkName,
                        projectNuGetFramework.DotNetFrameworkName);

                    var warning = RestoreLogMessage.CreateWarning(NuGetLogCode.NU1702, message);
                    warning.LibraryId = referencedProjectFile;
                    warning.ProjectPath = CurrentProjectName;

                    // log NU1702 for ATF on project reference
                    logger.Log(warning);

                    itemWithProperties.SetMetadata(NEAREST_TARGET_FRAMEWORK, nearestNuGetFramework);
                    return itemWithProperties;
                }
            }

            // no match found
            logger.LogError(string.Format(Strings.NoCompatibleTargetFramework, project.ItemSpec, CurrentProjectTargetFramework, referencedProjectFrameworkString));
            return itemWithProperties;
        }

        private static bool TryParseFramework(string framework, string errorMessage, MSBuildLogger logger, out NuGetFramework nugetFramework)
        {
            nugetFramework = NuGetFramework.Parse(framework);

            // validate framework
            if (nugetFramework.IsUnsupported)
            {
                logger.LogError(errorMessage);
                return false;
            }

            return true;
        }
    }
}
