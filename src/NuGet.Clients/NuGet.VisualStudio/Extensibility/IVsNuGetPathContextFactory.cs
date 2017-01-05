// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;

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
        /// Create an <see cref="IVsNuGetPathContext"/> based on the provided DTE project.
        /// </summary>
        /// <param name="project">The DTE project.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>The path context.</returns>
        Task<IVsNuGetPathContext> CreateAsync(Project project, CancellationToken token);
    }
}
