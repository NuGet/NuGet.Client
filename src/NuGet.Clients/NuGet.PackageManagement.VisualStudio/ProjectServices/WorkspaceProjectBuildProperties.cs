// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Workspace.Extensions.MSBuild;
using NuGet.ProjectManagement;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.VisualStudio
{
    internal class WorkspaceProjectBuildProperties
        : IProjectBuildProperties
    {
        private readonly AsyncLazy<IMSBuildProjectDataService> _buildProjectDataService;

        private readonly IDeferredProjectWorkspaceService _workspaceService;
        private readonly IVsProjectThreadingService _threadingService;
        private readonly string _fullProjectPath;

        public WorkspaceProjectBuildProperties(
            string fullProjectPath,
            IDeferredProjectWorkspaceService workspaceService,
            IVsProjectThreadingService threadingService)
        {
            Assumes.Present(workspaceService);
            Assumes.Present(threadingService);

            _workspaceService = workspaceService;
            _threadingService = threadingService;
            _fullProjectPath = fullProjectPath;

            _buildProjectDataService = new AsyncLazy<IMSBuildProjectDataService>(
                () => workspaceService.GetMSBuildProjectDataServiceAsync(_fullProjectPath),
                threadingService.JoinableTaskFactory);
        }

        public string GetPropertyValue(string propertyName)
        {
            return _threadingService.ExecuteSynchronously(() => GetPropertyValueAsync(propertyName));
        }

        public async Task<string> GetPropertyValueAsync(string propertyName)
        {
            Assumes.NotNullOrEmpty(propertyName);

            await TaskScheduler.Default;

            var dataService = await _buildProjectDataService.GetValueAsync();
            var propertyData = await dataService.GetProjectProperty(propertyName);
            return propertyData.EvaluatedValue;
        }
    }
}
