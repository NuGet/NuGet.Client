// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Commands;
using NuGet.Frameworks;

namespace NuGet.Build.Tasks
{
    public class GetReferenceNearestTargetFrameworkTask : Task
    {
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
            var log = new MSBuildLogger(Log);

            BuildTasksUtility.LogInputParam(log, nameof(CurrentProjectTargetFramework), CurrentProjectTargetFramework);
            BuildTasksUtility.LogInputParam(log, nameof(AnnotatedProjectReferences),
                AnnotatedProjectReferences == null
                    ? ""
                    : string.Join(";", AnnotatedProjectReferences.Select(p => p.ItemSpec)));

            if (AnnotatedProjectReferences == null)
            {
                return !Log.HasLoggedErrors;
            }

            var frameworkToMatch = NuGetFramework.Parse(CurrentProjectTargetFramework);
            if (frameworkToMatch.IsUnsupported)
            {
                log.LogError(string.Format(Strings.UnsupportedTargetFramework, CurrentProjectTargetFramework));
                return false;
            }

            AssignedProjects = new ITaskItem[AnnotatedProjectReferences.Length];
            for (var index = 0; index < AnnotatedProjectReferences.Length; index++)
            {
                AssignedProjects[index] = AssignPropertiesForSingleReference(AnnotatedProjectReferences[index], frameworkToMatch);
            }

            BuildTasksUtility.LogOutputParam(log, nameof(AssignedProjects), string.Join(";", AssignedProjects.Select(p => p.ItemSpec)));

            return !Log.HasLoggedErrors;
        }

        private ITaskItem AssignPropertiesForSingleReference(ITaskItem project, NuGetFramework currentProjectTargetFramework)
        {
            var itemWithProperties = new TaskItem(project);
            var targetFrameworks = project.GetMetadata("TargetFrameworks");

            if (string.IsNullOrEmpty(targetFrameworks))
            {
                // No target frameworks set, nothing to do.
                return itemWithProperties;
            }

            var possibleTargetFrameworks = MSBuildStringUtility.Split(targetFrameworks);
            var nearestNuGetFramework = NuGetFrameworkUtility.GetNearest(possibleTargetFrameworks, currentProjectTargetFramework, NuGetFramework.Parse);

            itemWithProperties.SetMetadata("NearestTargetFramework", nearestNuGetFramework ?? FrameworkConstants.SpecialIdentifiers.Unsupported);

            if (nearestNuGetFramework == null)
            {
                Log.LogError(string.Format(Strings.NoCompatibleTargetFramework, project.ItemSpec, CurrentProjectTargetFramework, targetFrameworks));
            }

            return itemWithProperties;
        }
    }
}
