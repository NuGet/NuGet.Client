// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.ProjectManagement;
using NuGet.Resolver;

namespace NuGet.PackageManagement.UI
{
    public class Options
    {
        public Options()
        {
            ShowPreviewWindow = true;
            CreateFileConflictActions();
            CreateDependencyBehaviors();
        }

        private void CreateFileConflictActions()
        {
            _fileConflicActions = new[]
                {
                    new FileConflictActionOptionItem(Resources.FileConflictAction_Prompt, FileConflictAction.PromptUser),
                    new FileConflictActionOptionItem(Resources.FileConflictAction_IgnoreAll, FileConflictAction.IgnoreAll),
                    new FileConflictActionOptionItem(Resources.FileConflictAction_OverwriteAll, FileConflictAction.OverwriteAll)
                };

            SelectedFileConflictAction = _fileConflicActions[0];
        }

        private void CreateDependencyBehaviors()
        {
            _dependencyBehaviors = new[]
                {
                    new DependencyBehaviorItem(Resources.DependencyBehavior_IgnoreDependencies, DependencyBehavior.Ignore),
                    new DependencyBehaviorItem(Resources.DependencyBehavior_Lowest, DependencyBehavior.Lowest),
                    new DependencyBehaviorItem(Resources.DependencyBehavior_HighestPatch, DependencyBehavior.HighestPatch),
                    new DependencyBehaviorItem(Resources.DependencyBehavior_HighestMinor, DependencyBehavior.HighestMinor),
                    new DependencyBehaviorItem(Resources.DependencyBehavior_Highest, DependencyBehavior.Highest)
                };
            SelectedDependencyBehavior = _dependencyBehaviors[1];
        }

        private FileConflictActionOptionItem[] _fileConflicActions;

        public IEnumerable<FileConflictActionOptionItem> FileConflictActions
        {
            get { return _fileConflicActions; }
        }

        public FileConflictActionOptionItem SelectedFileConflictAction { get; set; }

        private DependencyBehaviorItem[] _dependencyBehaviors;

        public IEnumerable<DependencyBehaviorItem> DependencyBehaviors
        {
            get { return _dependencyBehaviors; }
        }

        public DependencyBehaviorItem SelectedDependencyBehavior { get; set; }

        public bool ShowPreviewWindow { get; set; }

        public bool RemoveDependencies { get; set; }

        public bool ForceRemove { get; set; }
    }
}
