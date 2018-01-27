// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Commands;
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
        /// Optional list of target frameworks to be used as Asset Target Fallback frameworks.
        /// </summary>
        public string[] AssetTargetFallbackFrameworks { get; set; }

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

            BuildTasksUtility.LogInputParam(logger, nameof(AssetTargetFallbackFrameworks),
                AssetTargetFallbackFrameworks == null
                    ? ""
                    : string.Join(";", AssetTargetFallbackFrameworks.Select(p => p)));

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
            if (!TryParseAndAddFrameworkToList(CurrentProjectTargetFramework, frameworksToMatch, logger))
            {
                return false;
            }

            if (AssetTargetFallbackFrameworks != null &&
                AssetTargetFallbackFrameworks.Length > 0)
            {
                var fallbackFrameworks = new List<NuGetFramework>();
                foreach (var assetTargetFallbackFramework in AssetTargetFallbackFrameworks)
                {
                    // validate ATF project framework
                    if (!TryParseAndAddFrameworkToList(assetTargetFallbackFramework, frameworksToMatch, logger))
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
            itemWithProperties.SetMetadata(NEAREST_TARGET_FRAMEWORK, FrameworkConstants.SpecialIdentifiers.Unsupported);
            Log.LogError(string.Format(Strings.NoCompatibleTargetFramework, project.ItemSpec, CurrentProjectTargetFramework, targetFrameworks));
            return itemWithProperties;
        }

        private static bool TryParseAndAddFrameworkToList(string framework, IList<NuGetFramework> frameworkList, MSBuildLogger logger)
        {
            var nugetFramework = NuGetFramework.Parse(framework);

            // validate framework
            if (nugetFramework.IsUnsupported)
            {
                logger.LogError(string.Format(Strings.UnsupportedTargetFramework, framework));
                return false;
            }

            frameworkList.Add(nugetFramework);
            return true;
        }
    }
}
