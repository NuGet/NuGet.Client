// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Services.Common;
using NuGet.Common;
using NuGet.ProjectManagement;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.UI.Options
{
    public class ConfigPathsWindowViewModel
    {
        public ObservableCollection<ConfigPathsViewModel> ConfigPaths { get; private set; }

        public void SetConfigPaths()
        {
            IComponentModel componentModelMapping = NuGetUIThreadHelper.JoinableTaskFactory.Run(ServiceLocator.GetComponentModelAsync);
            var settings = componentModelMapping.GetService<Configuration.ISettings>();
            IReadOnlyList<string> configPaths = settings.GetConfigFilePaths().ToList();
            ConfigPaths.AddRange(CreateViewModels(configPaths));
        }

        private ObservableCollection<ConfigPathsViewModel> CreateViewModels(IReadOnlyList<string> configPaths)
        {
            var configPathsCollection = new ObservableCollection<ConfigPathsViewModel>();
            foreach (var configPath in configPaths)
            {
                var viewModel = new ConfigPathsViewModel(configPath);
                configPathsCollection.Add(viewModel);
            }

            return configPathsCollection;
        }

        public void OpenConfigFile(ConfigPathsViewModel selectedPath)
        {
            var componentModel = NuGetUIThreadHelper.JoinableTaskFactory.Run(ServiceLocator.GetComponentModelAsync);
            var projectContext = componentModel.GetService<INuGetProjectContext>();
            if (!File.Exists(selectedPath.ConfigPath))
            {
                var error = new FileNotFoundException(selectedPath.ConfigPath);
                var errorEvent = new TelemetryEvent("ConfigPathsFileNotFoundException");
                MessageHelper.ShowErrorMessage(error.Message, Resources.ShowError_FileNotFound);
                TelemetryActivity.EmitTelemetryEvent(errorEvent);
            }
            _ = projectContext.ExecutionContext.OpenFile(selectedPath.ConfigPath);

        }

        public ConfigPathsWindowViewModel()
        {
            ConfigPaths = new ObservableCollection<ConfigPathsViewModel>();
        }
    }
}
