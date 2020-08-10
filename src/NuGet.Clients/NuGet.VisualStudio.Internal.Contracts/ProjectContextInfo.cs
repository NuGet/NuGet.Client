// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using NuGet.ProjectModel;

namespace NuGet.VisualStudio.Internal.Contracts
{
    public sealed class ProjectContextInfo : IProjectContextInfo
    {
        public ProjectContextInfo(string projectUniqueId, ProjectStyle projectStyle, NuGetProjectKind projectKind)
        {
            UniqueId = projectUniqueId;
            ProjectStyle = projectStyle;
            ProjectKind = projectKind;
        }

        public string UniqueId { get; private set; }
        public NuGetProjectKind ProjectKind { get; private set; }
        public ProjectStyle ProjectStyle { get; private set; }
    }
}
