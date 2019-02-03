// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.ProjectModel;
using NuGet.Shared;
using NuGet.VisualStudio;

namespace NuGet.SolutionRestoreManager
{
    /// <summary>
    /// Implementation of the <see cref="IVsSolutionRestoreService"/> and <see cref="IVsSolutionRestoreService2"/>.
    /// Provides extension API for project restore nomination triggered by 3rd party component.
    /// Configured as a single-instance MEF part.
    /// </summary>
    [PartCreationPolicy(CreationPolicy.Shared)]
    [Export(typeof(IVsSolutionRestoreService))]
    [Export(typeof(IVsSolutionRestoreService2))]
    public sealed class VsSolutionRestoreService : IVsSolutionRestoreService, IVsSolutionRestoreService2
    {
        private readonly IProjectSystemCache _projectSystemCache;
        private readonly ISolutionRestoreWorker _restoreWorker;
        private readonly ILogger _logger;

        [ImportingConstructor]
        public VsSolutionRestoreService(
            IProjectSystemCache projectSystemCache,
            ISolutionRestoreWorker restoreWorker,
            [Import("VisualStudioActivityLogger")]
            ILogger logger)
        {
            _projectSystemCache = projectSystemCache ?? throw new ArgumentNullException(nameof(projectSystemCache));
            _restoreWorker = restoreWorker ?? throw new ArgumentNullException(nameof(restoreWorker));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<bool> CurrentRestoreOperation => _restoreWorker.CurrentRestoreOperation;

        public Task<bool> NominateProjectAsync(string projectUniqueName, CancellationToken token)
        {
            Assumes.NotNullOrEmpty(projectUniqueName);

            // returned task completes when scheduled restore operation completes.
            var restoreTask = _restoreWorker.ScheduleRestoreAsync(
                SolutionRestoreRequest.OnUpdate(),
                token);

            return restoreTask;
        }

        public Task<bool> NominateProjectAsync(string projectUniqueName, IVsProjectRestoreInfo projectRestoreInfo, CancellationToken token)
        {
            if (string.IsNullOrEmpty(projectUniqueName))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(projectUniqueName));
            }

            if (projectRestoreInfo == null)
            {
                throw new ArgumentNullException(nameof(projectRestoreInfo));
            }

            if (projectRestoreInfo.TargetFrameworks == null)
            {
                throw new InvalidOperationException("TargetFrameworks cannot be null.");
            }

            try
            {
                _logger.LogInformation(
                    $"The nominate API is called for '{projectUniqueName}'.");

                var projectNames = ProjectNames.FromFullProjectPath(projectUniqueName);

                var dgSpec = ToDependencyGraphSpec(projectNames, projectRestoreInfo);
                _projectSystemCache.AddProjectRestoreInfo(projectNames, dgSpec);

                // returned task completes when scheduled restore operation completes.
                var restoreTask = _restoreWorker.ScheduleRestoreAsync(
                    SolutionRestoreRequest.OnUpdate(),
                    token);

                return restoreTask;
            }
            catch (Exception e)
            when (e is InvalidOperationException || e is ArgumentException || e is FormatException)
            {
                _logger.LogError(e.ToString());
                return Task.FromResult(false);
            }
            catch (Exception e)
            {
                _logger.LogError(e.ToString());
                throw;
            }
        }

        private static DependencyGraphSpec ToDependencyGraphSpec(ProjectNames projectNames, IVsProjectRestoreInfo projectRestoreInfo)
        {
            var dgSpec = new DependencyGraphSpec();

            var packageSpec = ToPackageSpec(projectNames, projectRestoreInfo);
            dgSpec.AddRestore(packageSpec.RestoreMetadata.ProjectUniqueName);
            dgSpec.AddProject(packageSpec);

            if (projectRestoreInfo.ToolReferences != null)
            {
                VSNominationUtilities.ProcessToolReferences(projectNames, projectRestoreInfo, dgSpec);
            }

            return dgSpec;
        }

        private static PackageSpec ToPackageSpec(ProjectNames projectNames, IVsProjectRestoreInfo projectRestoreInfo)
        {
            var tfis = projectRestoreInfo
                .TargetFrameworks
                .Cast<IVsTargetFrameworkInfo>()
                .Select(VSNominationUtilities.ToTargetFrameworkInformation)
                .ToArray();

            var projectFullPath = Path.GetFullPath(projectNames.FullName);
            var projectDirectory = Path.GetDirectoryName(projectFullPath);
            
            // Initialize OTF and CT values when original value of OTF property is not provided.
            var originalTargetFrameworks = tfis
                .Select(tfi => tfi.FrameworkName.GetShortFolderName())
                .ToArray();
            var crossTargeting = originalTargetFrameworks.Length > 1;

            // if "TargetFrameworks" property presents in the project file prefer the raw value.
            if (!string.IsNullOrWhiteSpace(projectRestoreInfo.OriginalTargetFrameworks))
            {
                originalTargetFrameworks = MSBuildStringUtility.Split(
                    projectRestoreInfo.OriginalTargetFrameworks);
                // cross-targeting is always ON even in case of a single tfm in the list.
                crossTargeting = true;
            }

            var outputPath = Path.GetFullPath(
                                Path.Combine(
                                    projectDirectory,
                                    projectRestoreInfo.BaseIntermediatePath));

            var projectName = VSNominationUtilities.GetPackageId(projectNames, projectRestoreInfo.TargetFrameworks);

            var packageSpec = new PackageSpec(tfis)
            {
                Name = projectName,
                Version = VSNominationUtilities.GetPackageVersion(projectRestoreInfo.TargetFrameworks),
                FilePath = projectFullPath,
                RestoreMetadata = new ProjectRestoreMetadata
                {
                    ProjectName = projectName,
                    ProjectUniqueName = projectFullPath,
                    ProjectPath = projectFullPath,
                    OutputPath = outputPath,
                    ProjectStyle = ProjectStyle.PackageReference,
                    TargetFrameworks = projectRestoreInfo.TargetFrameworks
                        .Cast<IVsTargetFrameworkInfo>()
                        .Select(item => VSNominationUtilities.ToProjectRestoreMetadataFrameworkInfo(item, projectDirectory))
                        .ToList(),
                    OriginalTargetFrameworks = originalTargetFrameworks,
                    CrossTargeting = crossTargeting,

                    // Read project properties for settings. ISettings values will be applied later since
                    // this value is put in the nomination cache and ISettings could change.
                    PackagesPath = VSNominationUtilities.GetRestoreProjectPath(projectRestoreInfo.TargetFrameworks),
                    FallbackFolders = VSNominationUtilities.GetRestoreFallbackFolders(projectRestoreInfo.TargetFrameworks).AsList(),
                    Sources = VSNominationUtilities.GetRestoreSources(projectRestoreInfo.TargetFrameworks)
                                    .Select(e => new PackageSource(e))
                                    .ToList(),
                    ProjectWideWarningProperties = VSNominationUtilities.GetProjectWideWarningProperties(projectRestoreInfo.TargetFrameworks),
                    CacheFilePath = NoOpRestoreUtilities.GetProjectCacheFilePath(cacheRoot: outputPath, projectPath: projectFullPath),
                    RestoreLockProperties = VSNominationUtilities.GetRestoreLockProperties(projectRestoreInfo.TargetFrameworks)
                },
                RuntimeGraph = VSNominationUtilities.GetRuntimeGraph(projectRestoreInfo),
                RestoreSettings = new ProjectRestoreSettings() { HideWarningsAndErrors = true }
            };

            return packageSpec;
        }        
    }
}
