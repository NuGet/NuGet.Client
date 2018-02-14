// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Frameworks;

namespace NuGet.Build.Tasks
{
    public class GetReferenceNearestTargetFrameworkTask : Task
    {
        private const string NEAREST_TARGET_FRAMEWORK = "NearestTargetFramework";
        private const string TARGET_FRAMEWORKS = "TargetFrameworks";

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

            var frameworksToMatch = new List<NuGetFramework>();

            // validate current project framework
            var errorMessage = string.Format(Strings.UnsupportedTargetFramework, CurrentProjectTargetFramework);
            if (!TryParseAndAddFrameworkToList(CurrentProjectTargetFramework, frameworksToMatch, errorMessage, logger))
            {
                return false;
            }

            if (FallbackTargetFrameworks != null &&
                FallbackTargetFrameworks.Length > 0)
            {
                foreach (var fallbackFramework in FallbackTargetFrameworks)
                {
                    // validate ATF project framework
                    errorMessage = string.Format(Strings.UnsupportedFallbackFramework, fallbackFramework);
                    if (!TryParseAndAddFrameworkToList(fallbackFramework, frameworksToMatch, errorMessage, logger))
                    {
                        return false;
                    }
                }
            }

            AssignedProjects = new ITaskItem[AnnotatedProjectReferences.Length];
            for (var index = 0; index < AnnotatedProjectReferences.Length; index++)
            {
                AssignedProjects[index] = AssignNearestFrameworkForSingleReference(AnnotatedProjectReferences[index], frameworksToMatch);
            }

            BuildTasksUtility.LogOutputParam(logger, nameof(AssignedProjects), string.Join(";", AssignedProjects.Select(p => p.ItemSpec)));

            return !Log.HasLoggedErrors;
        }

        private ITaskItem AssignNearestFrameworkForSingleReference(ITaskItem project, IList<NuGetFramework> currentProjectTargetFrameworks)
        {
            var itemWithProperties = new TaskItem(project);
            var targetFrameworks = project.GetMetadata(TARGET_FRAMEWORKS);

            if (string.IsNullOrEmpty(targetFrameworks))
            {
                // No target frameworks set, nothing to do.
                return itemWithProperties;
            }

            var possibleTargetFrameworks = MSBuildStringUtility.Split(targetFrameworks);

            foreach (var currentProjectTargetFramework in currentProjectTargetFrameworks)
            {
                var nearestNuGetFramework = NuGetFrameworkUtility.GetNearest(possibleTargetFrameworks, currentProjectTargetFramework, NuGetFramework.Parse);

                if (nearestNuGetFramework != null)
                {
                    itemWithProperties.SetMetadata(NEAREST_TARGET_FRAMEWORK, nearestNuGetFramework);
                    return itemWithProperties;
                }
            }

            // no match found
            Log.LogError(string.Format(Strings.NoCompatibleTargetFramework, project.ItemSpec, CurrentProjectTargetFramework, targetFrameworks));
            return itemWithProperties;
        }

        private static bool TryParseAndAddFrameworkToList(string framework, IList<NuGetFramework> frameworkList, string errorMessage, MSBuildLogger logger)
        {
            var nugetFramework = NuGetFramework.Parse(framework);

            // validate framework
            if (nugetFramework.IsUnsupported)
            {
                logger.LogError(errorMessage);
                return false;
            }

            frameworkList.Add(nugetFramework);
            return true;
        }
    }
}
