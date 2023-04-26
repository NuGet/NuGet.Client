// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Configuration;
using NuGet.PackageManagement.VisualStudio;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.UI.ViewModels
{
    public sealed class PackageSourceMappingActionViewModel : ViewModelBase
    {
        private bool _isCreateNewMappingChecked;
        private bool _isPackageMapped;
        private string _sourceName;
        internal INuGetUI UIController { get; }

        private PackageSourceMappingActionViewModel()
        {
            IsApplicable = false;
        }

        private PackageSourceMappingActionViewModel(bool isEnabled, string sourceName, INuGetUI uiController)
        {
            IsApplicable = true;
            IsEnabledForProject = isEnabled;
            SourceName = sourceName;
            UIController = uiController;
        }

        /// <summary>
        /// Whether Package Source Mapping could be applicable to the current project, regardless of whether it's enabled.
        /// </summary>
        public bool IsApplicable { get; }

        public bool IsEnabledForProject { get; }

        public bool IsCreateNewMappingChecked
        {
            get { return _isCreateNewMappingChecked; }
            set { SetAndRaisePropertyChanged(ref _isCreateNewMappingChecked, value); }
        }

        public bool CanSelectedSourceBeMapped
        {
            get
            {
                return !string.IsNullOrWhiteSpace(SourceName)
                    && !string.Equals(SourceName, Strings.AggregateSourceName, StringComparison.OrdinalIgnoreCase);
            }
        }

        public bool IsPackageMapped
        {
            get { return _isPackageMapped; }
            set { SetAndRaisePropertyChanged(ref _isPackageMapped, value); }
        }

        public string SourceName
        {
            get { return _sourceName; }
            set
            {
                SetAndRaisePropertyChanged(ref _sourceName, value);
                RaisePropertyChanged(nameof(CanSelectedSourceBeMapped));
            }
        }

        public static PackageSourceMappingActionViewModel CreateNotApplicableViewModel()
        {
            return new PackageSourceMappingActionViewModel();
        }

        public static PackageSourceMappingActionViewModel Create(IEnumerable<IProjectContextInfo> projects, INuGetUI uiController)
        {
            // Only PackageReference is currently supported.
            if (projects != null
                && uiController != null
                && projects.Count() == 1
                && projects.First().ProjectStyle.Equals(ProjectModel.ProjectStyle.PackageReference))
            {
                bool isEnabled = false;
                if (uiController?.Settings != null)
                {
                    isEnabled = PackageSourceMapping.GetPackageSourceMapping(uiController.Settings).IsEnabled;
                }

                string selectedSourceName = uiController.ActivePackageSourceMoniker.SourceName;
                return new PackageSourceMappingActionViewModel(isEnabled, selectedSourceName, uiController);
            }
            else
            {
                return CreateNotApplicableViewModel();
            }
        }
    }
}
