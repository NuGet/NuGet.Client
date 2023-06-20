// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Services.Common;
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

        public ConfigPathsWindowViewModel()
        {
            ConfigPaths = new ObservableCollection<ConfigPathsViewModel>();
        }
    }
}
