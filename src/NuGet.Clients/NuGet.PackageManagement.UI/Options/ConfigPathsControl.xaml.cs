using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Services.Common;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.UI.Options
{
    /// <summary>
    /// Interaction logic for ConfigPathsControl.xaml
    /// </summary>
    public partial class ConfigPathsControl : UserControl
    {
        //private AddMappingDialog _addMappingDialog;
        public ObservableCollection<ConfigPathsViewModel> ConfigPaths { get; private set; }

        public ConfigPathsControl()
        {
            ConfigPaths = new ObservableCollection<ConfigPathsViewModel>();
            OpenConfigurationFile = new DelegateCommand(ExecuteOpenConfigurationFile, (object parameter) => true, NuGetUIThreadHelper.JoinableTaskFactory);
            DataContext = this;
            InitializeComponent();
        }

        private void ExecuteOpenConfigurationFile(object obj)
        {
            // open the file
            var selectedPath = (ConfigPathsViewModel)_configurationPaths.SelectedItem;
        }

        internal void InitializeOnActivated(CancellationToken cancellationToken)
        {
            // should caclculate the config files and create the view models
            // array of view models
            // each view model should represent a config file
            IComponentModel componentModelMapping = NuGetUIThreadHelper.JoinableTaskFactory.Run(ServiceLocator.GetComponentModelAsync);
            var settings = componentModelMapping.GetService<Configuration.ISettings>();
            List<string> configPaths = settings.GetConfigFilePaths().ToList();
            ConfigPaths.AddRange(CreateViewModels(configPaths));

            // ObservableCollection<string> a = settings.GetConfigFilePaths().ToList();
            //ConfigPaths = settings.GetConfigFilePaths().OfType<ObservableCollection<string>();
        }

        public ICommand OpenConfigurationFile { get; set; }

        private ObservableCollection<ConfigPathsViewModel> CreateViewModels(List<string> configPaths)
        {
            var configPathsCollection = new ObservableCollection<ConfigPathsViewModel>();
            foreach (var configPath in configPaths)
            {
                var viewModel = new ConfigPathsViewModel(configPath);
                configPathsCollection.Add(viewModel);
            }

            return configPathsCollection;
        }
    }
}
