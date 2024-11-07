// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Utilities.UnifiedSettings;

namespace NuGet.PackageManagement.VisualStudio.Services
{
    [Guid("6C09BBE2-4537-48B4-87D8-01BF5EB75901")]
    public sealed class ExternalSettingsProviderService : IExternalSettingsProvider
    {
        public ExternalSettingsProviderService()
        {
        }

        public event EventHandler<ExternalSettingsChangedEventArgs> SettingValuesChanged { add { } remove { } }
        public event EventHandler<EnumSettingChoicesChangedEventArgs> EnumSettingChoicesChanged { add { } remove { } }
        public event EventHandler<DynamicMessageTextChangedEventArgs> DynamicMessageTextChanged { add { } remove { } }
        public event EventHandler ErrorConditionResolved { add { } remove { } }

        public void Dispose()
        {
        }

        public Task<ExternalSettingOperationResult<IReadOnlyList<EnumChoice>>> GetEnumChoicesAsync(string enumSettingMoniker, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<string> GetMessageTextAsync(string messageId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<ExternalSettingOperationResult<T>> GetValueAsync<T>(string moniker, CancellationToken cancellationToken) where T : notnull
        {
            throw new NotImplementedException();
        }

        public Task OpenBackingStoreAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<ExternalSettingOperationResult> SetValueAsync<T>(string moniker, T value, CancellationToken cancellationToken) where T : notnull
        {
            throw new NotImplementedException();
        }

        //private async Task LoadAsync()
        //{
        //    //ISettingsManager settingsManager = await ServiceLocator.GetGlobalServiceAsync<SVsUnifiedSettingsManager, ISettingsManager>();
        //    //ISettingsReader reader = settingsManager.GetReader();

        //    //IVsSolutionManager solutionManager = await _sharedServiceState.SolutionManager.GetValueAsync();
        //    //return _sharedServiceState.SourceRepositoryProvider.CreateRepository(
        //    //    new PackageSource(PackagesFolderPathUtility.GetPackagesFolderPath(solutionManager, settings)),
        //    //    FeedType.FileSystemPackagesConfig);


        //    ISettings settings = await ServiceLocator.GetComponentModelServiceAsync<ISettings>();
        //}

    }
}
