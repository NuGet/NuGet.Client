// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.ProjectManagement;
using NuGet.Resolver;

namespace NuGet.PackageManagement.UI
{
    [Serializable]
    public class UserSettings
    {
        /// <summary>
        /// Represents the user settings persisted in suo files.
        /// </summary>
        public UserSettings()
        {
            IncludePrerelease = true;
            SelectedFilter = Filter.All;
        }

        public string SourceRepository { get; set; }

        public bool ShowPreviewWindow { get; set; }

        public bool RemoveDependencies { get; set; }

        public bool ForceRemove { get; set; }

        public bool IncludePrerelease { get; set; }

        public Filter SelectedFilter { get; set; }

        public DependencyBehavior DependencyBehavior { get; set; }

        public FileConflictAction FileConflictAction { get; set; }
    }
}
