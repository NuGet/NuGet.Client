// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.InteropServices;

namespace NuGet.VisualStudio
{
    /// <summary>
    /// A factory to initialize <see cref="IVsNuGetPathContext"/> instances.
    /// </summary>
    [ComImport]
    [Guid("5BAC7095-F674-4778-8788-E15FFF77F96B")]
    public interface IVsNuGetPathContextFactory
    {
        /// <summary>
        /// Create an <see cref="IVsNuGetPathContext"/> based on the given DTE project.
        /// </summary>
        /// <param name="project">The DTE project.</param>
        /// <returns>The path context.</returns>
        IVsNuGetPathContext Create(EnvDTE.Project project);
    }
}
