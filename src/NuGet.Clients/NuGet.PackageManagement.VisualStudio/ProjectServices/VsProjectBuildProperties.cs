// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.ProjectManagement;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Contains the information specific to a Visual Basic or C# project or NetCore project.
    /// </summary>
    internal class VsProjectBuildProperties
        : IProjectBuildProperties
    {
        private readonly EnvDTE.Project _project;
        private readonly IVsBuildPropertyStorage _propertyStorage;
        private readonly IVsProjectThreadingService _threadingService;

        public VsProjectBuildProperties(
            EnvDTE.Project project,
            IVsBuildPropertyStorage propertyStorage,
            IVsProjectThreadingService threadingService)
        {
            Assumes.Present(project);
            Assumes.Present(threadingService);

            _project = project;
            _propertyStorage = propertyStorage;
            _threadingService = threadingService;
        }

        public string GetPropertyValue(string propertyName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Assumes.NotNullOrEmpty(propertyName);
            if (_propertyStorage != null)
            {
                var result = _propertyStorage.GetPropertyValue(
                    pszPropName: propertyName,
                    pszConfigName: null,
                    storage: (uint)_PersistStorageType.PST_PROJECT_FILE,
                    pbstrPropValue: out string output);

                if (result == VSConstants.S_OK && !string.IsNullOrWhiteSpace(output))
                {
                    return output;
                }
            }

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

        public async Task<string> GetPropertyValueAsync(string propertyName)
        {
            await _threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();
            return GetPropertyValue(propertyName);
        }
    }
}
