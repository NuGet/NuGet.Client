// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Services.Common;
using NuGet.ProjectManagement;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.UI.Options
{
    public class ConfigPathsViewModel
    {
        public ObservableCollection<string> ConfigPathsCollection { get; private set; }

        public ConfigPathsViewModel()
        {
            ConfigPathsCollection = new ObservableCollection<string>();
        }

        public void SetConfigPaths()
        {
            IComponentModel componentModelMapping = NuGetUIThreadHelper.JoinableTaskFactory.Run(ServiceLocator.GetComponentModelAsync);
            var settings = componentModelMapping.GetService<Configuration.ISettings>();
            IReadOnlyList<string> configPaths = settings.GetConfigFilePaths().ToList();
            ConfigPathsCollection.AddRange(CreateViewModels(configPaths));
        }

        private ObservableCollection<string> CreateViewModels(IReadOnlyList<string> configPaths)
        {
            var configPathsCollection = new ObservableCollection<string>();
            foreach (var configPath in configPaths)
            {
                configPathsCollection.Add(configPath);
            }

            return configPathsCollection;
        }

        public void OpenConfigFile(string selectedPath)
        {
            var componentModel = NuGetUIThreadHelper.JoinableTaskFactory.Run(ServiceLocator.GetComponentModelAsync);
            var projectContext = componentModel.GetService<INuGetProjectContext>();

            // This check is performed in case the user moves or deletes a config file while they have it selected in the Options window.
            if (!File.Exists(selectedPath))
            {
                var error = new FileNotFoundException(selectedPath);
                MessageHelper.ShowErrorMessage(error.Message, Resources.ShowError_FileNotFound);
            }
            _ = projectContext.ExecutionContext.OpenFile(selectedPath);
        }
    }
}
