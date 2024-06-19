// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.VisualStudio.Services
{
    public sealed class ExternalSettingsProviderService
    {
        public ExternalSettingsProviderService()
        {
        }

        private async Task LoadAsync()
        {
            //ISettingsManager settingsManager = await ServiceLocator.GetGlobalServiceAsync<SVsUnifiedSettingsManager, ISettingsManager>();
            //ISettingsReader reader = settingsManager.GetReader();

            //IVsSolutionManager solutionManager = await _sharedServiceState.SolutionManager.GetValueAsync();
            //return _sharedServiceState.SourceRepositoryProvider.CreateRepository(
            //    new PackageSource(PackagesFolderPathUtility.GetPackagesFolderPath(solutionManager, settings)),
            //    FeedType.FileSystemPackagesConfig);


            ISettings settings = await ServiceLocator.GetComponentModelServiceAsync<ISettings>();
            settings.
        }

    }
}
