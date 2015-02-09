using System;
using NuGet.ProjectManagement;
using NuGet.Resolver;

namespace NuGet.PackageManagement.UI
{
    [Serializable]
    public class UserSettings
    {
        public string SourceRepository { get; set; }

        public bool ShowPreviewWindow { get; set; }

        public bool RemoveDependencies { get; set; }

        public bool ForceRemove { get; set; }

        public DependencyBehavior DependencyBehavior { get; set; }

        public FileConflictAction FileConflictAction { get; set; }
    }
}