// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft;
using NuGet.ProjectManagement;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Contains the information specific to a Visual Basic or C# project.
    /// </summary>
    internal class VsCoreProjectBuildProperties
        : IProjectBuildProperties
    {
        private readonly EnvDTE.Project _project;
        private readonly IVsProjectThreadingService _threadingService;

        public VsCoreProjectBuildProperties(
            EnvDTE.Project project,
            IVsProjectThreadingService threadingService)
        {
            Assumes.Present(project);
            Assumes.Present(threadingService);

            _project = project;
            _threadingService = threadingService;
        }

        public string GetPropertyValue(string propertyName)
        {
            return _threadingService.ExecuteSynchronously(() => GetPropertyValueAsync(propertyName));
        }

        public async Task<string> GetPropertyValueAsync(string propertyName)
        {
            Assumes.NotNullOrEmpty(propertyName);

            await _threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                var property = _project.Properties.Item(propertyName);
                return property?.Value as string;
            }
            catch (ArgumentException)
            {
                // If the property doesn't exist this will throw an argument exception
            }

            return null;
        }
    }
}
