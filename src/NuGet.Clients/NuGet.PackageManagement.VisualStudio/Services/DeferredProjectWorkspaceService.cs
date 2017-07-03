// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Workspace;
using Microsoft.VisualStudio.Workspace.Extensions.MSBuild;
using Microsoft.VisualStudio.Workspace.Indexing;
using Microsoft.VisualStudio.Workspace.VSIntegration;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.VisualStudio
{
    [Export(typeof(IDeferredProjectWorkspaceService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal sealed class DeferredProjectWorkspaceService : IDeferredProjectWorkspaceService
    {
        private readonly AsyncLazy<IVsSolutionWorkspaceService> _solutionWorkspaceService;

        private IVsSolutionWorkspaceService SolutionWorkspaceService => NuGetUIThreadHelper.JoinableTaskFactory.Run(_solutionWorkspaceService.GetValueAsync);

        [ImportingConstructor]
        public DeferredProjectWorkspaceService(
            [Import(typeof(SVsServiceProvider))]
            IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            _solutionWorkspaceService = new AsyncLazy<IVsSolutionWorkspaceService>(
                async () =>
                {
                    await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    return (IVsSolutionWorkspaceService)serviceProvider.GetService(typeof(SVsSolutionWorkspaceService));
                },
                NuGetUIThreadHelper.JoinableTaskFactory);
        }

        public async Task<IEnumerable<string>> GetProjectReferencesAsync(string projectFilePath)
        {
            var workspace = SolutionWorkspaceService.CurrentWorkspace;
            var indexService = workspace.GetIndexWorkspaceService();
            var fileReferenceResult = await indexService.GetFileReferencesAsync(projectFilePath, referenceTypes: (int)FileReferenceInfoType.ProjectReference);
            return fileReferenceResult.Select(f => workspace.MakeRooted(f.Path));
        }

        public async Task<IMSBuildProjectDataService> GetMSBuildProjectDataServiceAsync(
            string projectFilePath, string targetFramework)
        {
            Assumes.NotNullOrEmpty(projectFilePath);

            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var factory = SolutionWorkspaceService.GetService<IVsSolutionMSBuildProjectServiceFactory>();
            Assumes.Present(factory);

            if (string.IsNullOrEmpty(targetFramework))
            {
                return await factory.GetMSBuildProjectDataServiceAsync(projectFilePath);
            }
            else
            {
                var projectProperties = new Dictionary<string, string>
                    {
                        { "TargetFramework", targetFramework }
                    };
                return await factory.GetMSBuildProjectDataServiceAsync(
                    projectFilePath, projectProperties: projectProperties);
            }
        }

        public async Task<string> GetProjectTypeGuidAsync(string projectFilePath)
        {
            var workspace = SolutionWorkspaceService.CurrentWorkspace;
            var solutionPath = SolutionWorkspaceService.SolutionFile;
            var indexService = workspace.GetIndexWorkspaceService();
            var indexedSolutionProjectTypes = (await indexService.GetFileDataValuesAsync<ProjectBaseTypesInSolution>(solutionPath,
                ProjectBaseTypesInSolution.TypeGuid, refreshOption: true)).FirstOrDefault();

            if (indexedSolutionProjectTypes != null)
            {
                var relativeProjectPath = Common.PathUtility.GetRelativePath(solutionPath, projectFilePath);

                if (!string.IsNullOrEmpty(relativeProjectPath)
                    && indexedSolutionProjectTypes.Value.Types.TryGetValue(relativeProjectPath, out Guid projectTypeGuid))
                {
                    return projectTypeGuid.ToString("B");
                }
            }

            return string.Empty;
        }
    }
}