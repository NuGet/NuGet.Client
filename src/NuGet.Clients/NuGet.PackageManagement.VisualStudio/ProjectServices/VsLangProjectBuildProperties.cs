// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.ProjectManagement;
using Task = System.Threading.Tasks.Task;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Contains the information specific to a Visual Basic or C# project.
    /// </summary>
    internal class VsLangProjectBuildProperties
        : IProjectBuildProperties
    {
        private readonly IVsBuildPropertyStorage _propertyStorage;
        private readonly IVsProjectThreadingService _threadingService;

        public VsLangProjectBuildProperties(
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
            _threadingService.ThrowIfNotOnUIThread();

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

        public Task<string> GetPropertyValueAsync(string propertyName)
        {
            return Task.FromResult(GetPropertyValue(propertyName));
        }
    }
}
