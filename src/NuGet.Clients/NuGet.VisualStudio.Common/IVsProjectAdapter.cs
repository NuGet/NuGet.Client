// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.Frameworks;
using NuGet.ProjectManagement;
using NuGet.RuntimeModel;

namespace NuGet.VisualStudio
{
    /// <summary>
    /// Represents an abstraction over Visual Studio project object. Provides access to project's properties and capabilities.
    /// </summary>
    public interface IVsProjectAdapter
    {
        /// <summary>
        /// BaseIntermediateOutputPath project property (e.g. c:\projFoo\obj)
        /// </summary>
        string BaseIntermediateOutputPath { get; }

        IProjectBuildProperties BuildProperties { get; }

        string CustomUniqueName { get; }

        string FullName { get; }

        string FullPath { get; }

        string FullProjectPath { get; }

        bool IsDeferred { get; }

        bool IsSupported { get; }

        /// <summary>
        /// PackageTargetFallback project property
        /// </summary>
        string PackageTargetFallback { get; }

        /// <summary>
        /// AssetTargetFallback project property
        /// </summary>
        string AssetTargetFallback { get; }

        /// <summary>
        /// Restore Packages Path DTE property
        /// </summary>
        string RestorePackagesPath { get; }

        /// <summary>
        /// Restore Sources DTE property
        /// </summary>
        string RestoreSources { get; }

        /// <summary>
        /// RestoreFallbackFolders DTE property
        /// </summary>
        string RestoreFallbackFolders { get; }

        /// <summary>
        /// In unavoidable circumstances where we need to DTE object, it's exposed here
        /// </summary>
        EnvDTE.Project Project { get; }

        string ProjectId { get; }

        string ProjectName { get; }

        ProjectNames ProjectNames { get; }

        string[] ProjectTypeGuids { get; }

        string UniqueName { get; }

        /// <summary>
        /// Version
        /// </summary>
        string Version { get; }

        IVsHierarchy VsHierarchy { get; }

        Task<bool> EntityExistsAsync(string filePath);

        Task<IEnumerable<string>> GetReferencedProjectsAsync();

        /// <summary>
        /// Project's runtime identifiers. Should never be null but can be an empty sequence.
        /// </summary>
        Task<IEnumerable<RuntimeDescription>> GetRuntimeIdentifiersAsync();

        /// <summary>
        /// Project's supports (a.k.a guardrails). Should never be null but can be an empty sequence.
        /// </summary>
        Task<IEnumerable<CompatibilityProfile>> GetRuntimeSupportsAsync();

        /// <summary>
        /// Project's target framework
        /// </summary>
        Task<NuGetFramework> GetTargetFrameworkAsync();
    }
}
