// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Linq;
using ContractsItemFilter = NuGet.VisualStudio.Internal.Contracts.ItemFilter;

namespace NuGet.PackageManagement.UI.ViewModels
{
    public sealed class PackageSourceMappingActionViewModel : ViewModelBase
    {
        internal INuGetUI UIController { get; }
        internal ContractsItemFilter ActiveTab { get; }

        private PackageSourceMappingActionViewModel(INuGetUI uiController, ContractsItemFilter contractsItemFilter)
        {
            UIController = uiController ?? throw new ArgumentNullException(nameof(uiController));
            ActiveTab = contractsItemFilter;
        }

        public bool IsPackageSourceMappingEnabled => UIController.UIContext?.PackageSourceMapping?.IsEnabled == true;

        private string? _packageId;

        internal string? PackageId
        {
            get { return _packageId; }
            set
            {
                if (_packageId == value)
                {
                    return;
                }

                _packageId = value;

                RaisePropertyChanged(nameof(IsPackageMapped));
                RaisePropertyChanged(nameof(MappingStatus));
            }
        }

        public bool IsPackageMapped
        {
            get
            {
                if (string.IsNullOrWhiteSpace(PackageId))
                {
                    return false;
                }

                var packageSourceMapping = UIController.UIContext.PackageSourceMapping;
                return packageSourceMapping?.GetConfiguredPackageSources(PackageId)?.Any() == true;
            }
        }

        public string MappingStatus
        {
            get
            {
                if (!IsPackageSourceMappingEnabled)
                {
                    return Resources.Text_PackageMappingsDisabled;
                }
                if (IsPackageMapped)
                {
                    return Resources.Text_PackageMappingsFound;
                }
                else
                {
                    return Resources.Text_PackageMappingsNotFound;
                }
            }
        }

        public void SettingsChanged()
        {
            RaisePropertyChanged(nameof(IsPackageSourceMappingEnabled));
            RaisePropertyChanged(nameof(IsPackageMapped));
            RaisePropertyChanged(nameof(MappingStatus));
        }

        public static PackageSourceMappingActionViewModel Create(INuGetUI uiController, ItemFilter itemFilter)
        {
            var contractsItemFilter = UIUtility.ToContractsItemFilter(itemFilter);
            return new PackageSourceMappingActionViewModel(uiController, contractsItemFilter);
        }
    }
}
