// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Linq;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using NuGet.Configuration;

namespace NuGet.PackageManagement.UI.ViewModels
{
    public sealed class PackageSourceMappingActionViewModel : ViewModelBase, IDisposable
    {
        private readonly ISettings _settings;

        internal INuGetUI UIController { get; }

        private PackageSourceMappingActionViewModel(INuGetUI uiController, ISettings settings)
        {
            UIController = uiController ?? throw new ArgumentNullException(nameof(uiController));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _settings.SettingsChanged += Settings_SettingsChanged;
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
                RaisePropertyChanged(nameof(MappingStatusIcon));
            }
        }

        internal bool _isPackageMapped;
        public bool IsPackageMapped
        {
            get
            {
                if (string.IsNullOrWhiteSpace(PackageId))
                {
                    _isPackageMapped = false;
                }
                else
                {
                    var packageSourceMapping = UIController.UIContext.PackageSourceMapping;
                    _isPackageMapped = packageSourceMapping.GetConfiguredPackageSources(PackageId!).Any() == true;
                }
                return _isPackageMapped;
            }
        }

        public bool CanAutomaticallyCreateSourceMapping
        {
            get
            {
                return IsPackageSourceMappingEnabled
                    && !IsPackageMapped
                    && ProjectsSupportAutomaticSourceMapping
                    && UIController.ActivePackageSourceMoniker != null
                    && !UIController.ActivePackageSourceMoniker.IsAggregateSource;
            }
        }

        internal bool ProjectsSupportAutomaticSourceMapping => !UIController.UIContext.Projects.Any(project => project.ProjectStyle != ProjectModel.ProjectStyle.PackageReference);

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

                if (CanAutomaticallyCreateSourceMapping)
                {
                    return Resources.Text_PackageMappingsAutoCreate;
                }

                return Resources.Text_PackageMappingsNotFound;
            }
        }

        public ImageMoniker MappingStatusIcon
        {
            get
            {
                if (!IsPackageSourceMappingEnabled)
                {
                    return KnownMonikers.StatusInformation;
                }
                if (IsPackageMapped)
                {
                    return KnownMonikers.StatusOK;
                }
                if (CanAutomaticallyCreateSourceMapping)
                {
                    return KnownMonikers.StatusInformation;
                }

                return KnownMonikers.StatusError;
            }
        }

        public void UpdateState()
        {
            RaisePropertyChanged(nameof(IsPackageSourceMappingEnabled));
            RaisePropertyChanged(nameof(IsPackageMapped));
            RaisePropertyChanged(nameof(MappingStatus));
            RaisePropertyChanged(nameof(MappingStatusIcon));
            RaisePropertyChanged(nameof(CanAutomaticallyCreateSourceMapping));
        }

        public void Dispose()
        {
            _settings.SettingsChanged -= Settings_SettingsChanged;
        }

        public static PackageSourceMappingActionViewModel Create(INuGetUI uiController, ISettings settings)
        {
            return new PackageSourceMappingActionViewModel(uiController, settings);
        }

        private void Settings_SettingsChanged(object sender, EventArgs e) => UpdateState();
    }
}
