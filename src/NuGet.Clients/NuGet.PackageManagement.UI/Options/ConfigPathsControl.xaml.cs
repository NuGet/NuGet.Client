using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Services.Common;
using NuGet.ProjectManagement;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.UI.Options
{
    /// <summary>
    /// Interaction logic for ConfigPathsControl.xaml
    /// </summary>
    public partial class ConfigPathsControl : UserControl
    {
        public ObservableCollection<ConfigPathsViewModel> ConfigPaths { get; private set; }
        private ConfigPathsWindowViewModel _configPathsWindow = new ConfigPathsWindowViewModel();

        public ConfigPathsControl()
        {
            ConfigPaths = _configPathsWindow.ConfigPaths;
            OpenConfigurationFile = new DelegateCommand(ExecuteOpenConfigurationFile, (object parameter) => true, NuGetUIThreadHelper.JoinableTaskFactory);
            DataContext = this;
            InitializeComponent();
        }

        private void ExecuteOpenConfigurationFile(object obj)
        {
            var selectedPath = (ConfigPathsViewModel)_configurationPaths.SelectedItem;
            var componentModel = NuGetUIThreadHelper.JoinableTaskFactory.Run(ServiceLocator.GetComponentModelAsync);
            var projectContext = componentModel.GetService<INuGetProjectContext>();
            _ = projectContext.ExecutionContext.OpenFile(selectedPath.ConfigPath);
        }

        internal void InitializeOnActivated(CancellationToken cancellationToken)
        {
            ConfigPaths.Clear();
            ConfigPaths = _configPathsWindow.GetConfigPaths(ConfigPaths);
        }

        public ICommand OpenConfigurationFile { get; set; }
    }
}
