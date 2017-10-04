// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Commands;
using NuGet.Frameworks;

namespace NuGet.Build.Tasks
{
    public class AssignReferenceProperties : Task
    {
        public ITaskItem[] AnnotatedProjectReferences { get; set; }

        [Required]
        public string CurrentProjectTargetFramework { get; set; }

        [Output]
        public ITaskItem[] AssignedProjects { get; set; }

        public override bool Execute()
        {
            if (AnnotatedProjectReferences == null)
            {
                return !Log.HasLoggedErrors;
            }

            var assignedProjects = new List<ITaskItem>(AnnotatedProjectReferences.Length);

            var frameworkToMatch = NuGetFramework.Parse(CurrentProjectTargetFramework);

            if (frameworkToMatch.IsUnsupported)
            {
                // TODO: Log unsupported framework match error.CurrentProjectTargetFramework will be 'unsupported'
                return false;
            }

            foreach (var project in AnnotatedProjectReferences)
            {
                assignedProjects.Add(AssignPropertiesForSingleReference(project, frameworkToMatch));
            }

            AssignedProjects = assignedProjects.ToArray();

            return !Log.HasLoggedErrors;
        }

        private ITaskItem AssignPropertiesForSingleReference(ITaskItem project, NuGetFramework currentProjectTargetFramework)
        {
            var itemWithProperties = new TaskItem(project);

            var possibleTargetFrameworks = MSBuildStringUtility.Split(project.GetMetadata("TargetFrameworks"));

            var possibleNuGetFrameworks = possibleTargetFrameworks.Select(ParseFramework).ToList();
            var nearestNuGetFramework = NuGetFrameworkUtility.GetNearest(possibleTargetFrameworks, currentProjectTargetFramework, e => NuGetFramework.Parse(e));

            // no longer needed, using NuGetFrameworkUtility.GetNearest 
            //var nearestNuGetFramework = new FrameworkReducer().GetNearest(currentProjectTargetFramework, possibleNuGetFrameworks);

            if (nearestNuGetFramework != null)
            {
                // Note that there can be more than one spelling of the same target framework (e.g. net45 and net4.5) and 
                // we must return a value that is spelled exactly the same way as the input. To 
                // achieve this, we find the index of the returned framework among the set we passed to NuGet and use that
                // to retrieve a value at the same position in the input.
                //
                // This is required to guarantee that a project can use whatever spelling appears in $(TargetFrameworks)
                // in a condition that compares against $(TargetFramework).
                //Log.LogError(Strings.NoCompatibleTargetFramework, ProjectFilePath, ReferringTargetFramework, string.Join("; ", possibleNuGetFrameworks));

                // AG: This used to get the original string back, but that's not needed since now with NuGetFrameworkUtility.GetNearest
                // AG: method which returns the original string back when specifying the selector ( e => NuGetFramework.Parse(e) )
                //var indexOfNearestFramework = possibleNuGetFrameworks.IndexOf(nearestNuGetFramework);
                //var nearestTargetFramework = possibleTargetFrameworks[indexOfNearestFramework];

                if (TryConvertItemMetadataToBool(project, "HasSingleTargetFramework", out bool singleTargetFramework) && singleTargetFramework)
                {
                    itemWithProperties.SetMetadata("UndefineProperties", "TargetFramework"); // TODO: append
                }
                else
                {
                    // TODO: append or overwrite?
                    itemWithProperties.SetMetadata("SetTargetFramework", $"TargetFramework={nearestNuGetFramework}");
                }

                itemWithProperties.SetMetadata("SkipGetTargetFrameworkProperties", "true");
            }

            return itemWithProperties;
        }

        private NuGetFramework ParseFramework(string name)
        {
            var framework = NuGetFramework.Parse(name);

            if (framework == null)
            {
                //Log.LogError(Strings.InvalidFrameworkName, framework);
            }

            return framework;
        }

        /// <summary>
        /// Convert a task item metadata to bool. 
        /// If the metadata is not found, then set metadataFound to false and then return false.
        /// </summary>
        /// <param name="item">The item that contains the metadata.</param>
        /// <param name="itemMetadataName">The name of the metadata.</param>
        /// <param name="metadataFound">Receives true if the metadata was found, false otherwise.</param>
        /// <returns>The resulting boolean value.</returns>
        internal static bool TryConvertItemMetadataToBool(ITaskItem item, string itemMetadataName,
            out bool metadataFound)
        {
            var metadataValue = item.GetMetadata(itemMetadataName);
            if (string.IsNullOrEmpty(metadataValue))
            {
                metadataFound = false;
                return false;
            }
            metadataFound = true;

            return MSBuildStringUtility.IsTrue(metadataValue);
        }
    }
}
