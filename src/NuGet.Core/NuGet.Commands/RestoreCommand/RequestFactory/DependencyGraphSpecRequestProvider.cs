// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.ProjectModel;

namespace NuGet.Commands
{
    /// <summary>
    /// In Memory dg file provider.
    /// </summary>
    public class DependencyGraphSpecRequestProvider : IPreLoadedRestoreRequestProvider
    {
        private const bool DefaultRestoreLegacyPackagesDirectory = false;

        private readonly DependencyGraphSpec _dgFile;
        private readonly RestoreCommandProvidersCache _providerCache;
        private readonly Dictionary<string, PackageSpec> _projectJsonCache = new Dictionary<string, PackageSpec>(StringComparer.Ordinal);
        private readonly ISettings _providerSettingsOverride;

        public DependencyGraphSpecRequestProvider(
            RestoreCommandProvidersCache providerCache,
            DependencyGraphSpec dgFile)
            : this(providerCache, dgFile, settingsOverride: null)
        {
        }

        public DependencyGraphSpecRequestProvider(
            RestoreCommandProvidersCache providerCache,
            DependencyGraphSpec dgFile,
            ISettings settingsOverride)
        {
            _dgFile = dgFile;
            _providerCache = providerCache;
            _providerSettingsOverride = settingsOverride;
        }

        public Task<IReadOnlyList<RestoreSummaryRequest>> CreateRequests(RestoreArgs restoreContext)
        {
            var requests = GetRequestsFromItems(restoreContext, _dgFile);

            return Task.FromResult<IReadOnlyList<RestoreSummaryRequest>>(requests);
        }

        private IReadOnlyList<RestoreSummaryRequest> GetRequestsFromItems(RestoreArgs restoreContext, DependencyGraphSpec dgFile)
        {
            if (restoreContext == null)
            {
                throw new ArgumentNullException(nameof(restoreContext));
            }

            if (dgFile == null)
            {
                throw new ArgumentNullException(nameof(dgFile));
            }

            // Write the dg file to disk of the NUGET_PERSIST_DG is set.
            MSBuildRestoreUtility.PersistDGFileIfDebugging(dgFile, restoreContext.Log);

            // Validate the dg file input, this throws if errors are found.
            SpecValidationUtility.ValidateDependencySpec(dgFile);

            // Create requests
            var requests = new List<RestoreSummaryRequest>();
            var toolRequests = new List<RestoreSummaryRequest>();

            foreach (var projectNameToRestore in dgFile.Restore)
            {
                var closure = dgFile.GetClosure(projectNameToRestore);

                var externalClosure = new HashSet<ExternalProjectReference>(closure.Select(GetExternalProject));

                var rootProject = externalClosure.Single(p =>
                    StringComparer.Ordinal.Equals(projectNameToRestore, p.UniqueName));

                var request = Create(rootProject, externalClosure, restoreContext, settingsOverride: _providerSettingsOverride);

                if (request.Request.ProjectStyle == ProjectStyle.DotnetCliTool)
                {
                    // Store tool requests to be filtered later
                    toolRequests.Add(request);
                }
                else
                {
                    requests.Add(request);
                }
            }

            // Filter out duplicate tool restore requests
            requests.AddRange(ToolRestoreUtility.GetSubSetRequests(toolRequests));

            return requests;
        }

        public static IEnumerable<ExternalProjectReference> GetExternalClosure(DependencyGraphSpec dgFile, string projectNameToRestore)
        {
            var closure = dgFile.GetClosure(projectNameToRestore);

            var externalClosure = closure.Select(GetExternalProject);
            return externalClosure;

        }

        private static ExternalProjectReference GetExternalProject(PackageSpec rootProject)
        {
            var projectReferences = rootProject.RestoreMetadata?.TargetFrameworks.SelectMany(e => e.ProjectReferences)
                ?? new List<ProjectRestoreReference>();

            var type = rootProject.RestoreMetadata?.ProjectStyle ?? ProjectStyle.Unknown;

            // Leave the spec null for non-nuget projects.
            // In the future additional P2P TFM checking could be handled by
            // creating a spec for non-NuGet projects and including the TFM.
            PackageSpec projectSpec = null;

            if (type == ProjectStyle.PackageReference
                || type == ProjectStyle.ProjectJson
                || type == ProjectStyle.DotnetCliTool
                || type == ProjectStyle.Standalone)
            {
                projectSpec = rootProject;
            }

            var uniqueReferences = projectReferences
                .Select(p => p.ProjectUniqueName)
                .Distinct(StringComparer.OrdinalIgnoreCase);

            return new ExternalProjectReference(
                rootProject.RestoreMetadata.ProjectUniqueName,
                projectSpec,
                rootProject.RestoreMetadata?.ProjectPath,
                uniqueReferences);
        }

        private RestoreSummaryRequest Create(
            ExternalProjectReference project,
            HashSet<ExternalProjectReference> projectReferenceClosure,
            RestoreArgs restoreContext,
            ISettings settingsOverride)
        {
            // Get settings relative to the input file
            var rootPath = Path.GetDirectoryName(project.PackageSpec.FilePath);

            var settings = settingsOverride;

            if (settings == null)
            {
                settings = restoreContext.GetSettings(rootPath);
            }

            var globalPath = restoreContext.GetEffectiveGlobalPackagesFolder(rootPath, settings);
            var fallbackPaths = restoreContext.GetEffectiveFallbackPackageFolders(settings);

            var sources = restoreContext.GetEffectiveSources(settings);

            var sharedCache = _providerCache.GetOrCreate(
                globalPath,
                fallbackPaths,
                sources,
                restoreContext.CacheContext,
                restoreContext.Log);

            // Create request
            var request = new RestoreRequest(
                project.PackageSpec,
                sharedCache,
                restoreContext.CacheContext,
                restoreContext.Log);

            // Set properties from the restore metadata
            request.ProjectStyle = project.PackageSpec?.RestoreMetadata?.ProjectStyle ?? ProjectStyle.Unknown;
            request.RestoreOutputPath = project.PackageSpec?.RestoreMetadata?.OutputPath ?? rootPath;
            var restoreLegacyPackagesDirectory = project.PackageSpec?.RestoreMetadata?.LegacyPackagesDirectory
                ?? DefaultRestoreLegacyPackagesDirectory;
            request.IsLowercasePackagesDirectory = !restoreLegacyPackagesDirectory;

            // Standard properties
            restoreContext.ApplyStandardProperties(request);

            // Add project references
            request.ExternalProjects = projectReferenceClosure.ToList();

            // The lock file is loaded later since this is an expensive operation
            var summaryRequest = new RestoreSummaryRequest(
                request,
                project.MSBuildProjectPath,
                settings,
                sources);

            return summaryRequest;
        }

        /// <summary>
        /// Return all references for a given project path.
        /// References is modified by this method.
        /// This includes the root project.
        /// </summary>
        private static void CollectReferences(
            ExternalProjectReference root,
            Dictionary<string, ExternalProjectReference> allProjects,
            HashSet<ExternalProjectReference> references)
        {
            if (references.Add(root))
            {
                foreach (var child in root.ExternalProjectReferences)
                {
                    ExternalProjectReference childProject;
                    if (!allProjects.TryGetValue(child, out childProject))
                    {
                        // Let the resolver handle this later
                        Debug.Fail($"Missing project {childProject}");
                    }

                    // Recurse down
                    CollectReferences(childProject, allProjects, references);
                }
            }
        }
    }
}