// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.ProjectManagement;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Contains the information specific to a Visual Basic or C# project.
    /// </summary>
    internal class VsManagedLanguagesProjectBuildProperties
        : IProjectBuildProperties
    {
        private readonly IVsBuildPropertyStorage _propertyStorage;
        private readonly IVsProjectThreadingService _threadingService;

        public VsManagedLanguagesProjectBuildProperties(
            IVsBuildPropertyStorage propertyStorage,
            IVsProjectThreadingService threadingService)
        {
            Assumes.Present(propertyStorage);
            Assumes.Present(threadingService);
            
            _propertyStorage = propertyStorage;
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

            string output = null;
            var result = _propertyStorage.GetPropertyValue(
                pszPropName: propertyName,
                pszConfigName: null,
                storage: (uint)_PersistStorageType.PST_PROJECT_FILE,
                pbstrPropValue: out output);

            if (result != VSConstants.S_OK || string.IsNullOrWhiteSpace(output))
            {
                return null;
            }

            return output;
        }
    }
}
