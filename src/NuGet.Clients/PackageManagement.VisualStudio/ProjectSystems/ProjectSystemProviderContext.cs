// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.ProjectManagement;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Additional data passed to <see cref="IProjectSystemProvider"/> method 
    /// to create project instance.
    /// </summary>
    public class ProjectSystemProviderContext
    {
        public INuGetProjectContext ProjectContext { get; }
        public Func<string> PackagesPathFactory { get; }

        public string NuGetProjectStyle { get; }

        public ProjectSystemProviderContext(
            INuGetProjectContext projectContext,
            Func<string> packagesPathFactory,
            string nuGetProjectStyle)
        {
            if (projectContext == null)
            {
                throw new ArgumentNullException(nameof(projectContext));
            }

            if (packagesPathFactory == null)
            {
                throw new ArgumentNullException(nameof(packagesPathFactory));
            }

            ProjectContext = projectContext;
            PackagesPathFactory = packagesPathFactory;
            NuGetProjectStyle = nuGetProjectStyle;
        }
    }
}
