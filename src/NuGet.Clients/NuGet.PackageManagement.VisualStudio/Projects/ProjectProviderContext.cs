// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.ProjectManagement;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Additional data passed to <see cref="INuGetProjectProvider"/> method 
    /// to create project instance.
    /// </summary>
    public class ProjectProviderContext
    {
        public INuGetProjectContext ProjectContext { get; }

        public Func<string> PackagesPathFactory { get; }

        public ProjectProviderContext(
            INuGetProjectContext projectContext,
            Func<string> packagesPathFactory)
        {
            ProjectContext = projectContext ?? throw new ArgumentNullException(nameof(projectContext));
            PackagesPathFactory = packagesPathFactory ?? throw new ArgumentNullException(nameof(packagesPathFactory));
        }
    }
}
