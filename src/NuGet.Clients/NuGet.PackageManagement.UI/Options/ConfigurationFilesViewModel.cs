// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
#nullable enable

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Services.Common;
using NuGet.ProjectManagement;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.UI.Options
{
    public class ConfigurationFilesViewModel
    {
        public ObservableCollection<string> ConfigurationFilesCollection { get; private set; }
        public string? SelectedPath { get; set; }
        public ICommand OpenConfigurationFile { get; }

        public ConfigurationFilesViewModel()
        {
            ConfigurationFilesCollection = new ObservableCollection<string>();
            OpenConfigurationFile = new DelegateCommand(ExecuteOpenConfigurationFile, IsSelectedPath, NuGetUIThreadHelper.JoinableTaskFactory);
        }

        private bool IsSelectedPath()
        {
            return SelectedPath != null;
        }

        private void ExecuteOpenConfigurationFile()
        {
            OpenConfigFile(SelectedPath);
        }

        public void SetConfigPaths()
        {
            IComponentModel componentModelMapping = NuGetUIThreadHelper.JoinableTaskFactory.Run(ServiceLocator.GetComponentModelAsync);
            var settings = componentModelMapping.GetService<Configuration.ISettings>();
            IReadOnlyList<string> configPaths = settings.GetConfigFilePaths().ToList();
            ConfigurationFilesCollection.Clear();
            ConfigurationFilesCollection.AddRange(configPaths);
        }

        private void OpenConfigFile(string? selectedPath)
        {
            var componentModel = NuGetUIThreadHelper.JoinableTaskFactory.Run(ServiceLocator.GetComponentModelAsync);
            var projectContext = componentModel.GetService<INuGetProjectContext>();

            // This check is performed in case the user moves or deletes a config file while they have it selected in the Options window.
            if (!File.Exists(selectedPath))
            {
                MessageHelper.ShowErrorMessage(selectedPath, Resources.ShowError_FileNotFound);
            }
            _ = projectContext.ExecutionContext.OpenFile(selectedPath);
        }
    }
}
