// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Provides API for creating <see cref="IVsProjectAdapter"/> instances.
    /// </summary>
    public interface IVsProjectAdapterProvider
    {
        /// <summary>
        /// Check if given file path exists from AnyCode api in LSL mode.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns>true, if file path exists</returns>
        Task<bool> EntityExistsAsync(string filePath);

        /// <summary>
        /// Creates a project adapter for fully loaded project represented by DTE object.
        /// </summary>
        /// <param name="dteProject">Input project object</param>
        /// <returns>New instance of project adapter encapsulating DTE project.</returns>
        Task<IVsProjectAdapter> CreateAdapterForFullyLoadedProjectAsync(EnvDTE.Project dteProject);

        /// <summary>
        /// Creates a project adapter for fully loaded project represented by DTE object.
        /// </summary>
        /// <param name="dteProject">Input project object</param>
        /// <returns>New instance of project adapter encapsulating DTE project.</returns>
        IVsProjectAdapter CreateAdapterForFullyLoadedProject(EnvDTE.Project dteProject);
    }
}
