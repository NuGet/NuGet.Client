using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml;
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
        public ConfigPathsWindowViewModel ConfigPathsWindow { get; set; }
        public ConfigPathsViewModel SelectedPath { get; set; }

        public ConfigPathsControl()
        {
            ConfigPathsWindow = new ConfigPathsWindowViewModel();
            OpenConfigurationFile = new DelegateCommand(ExecuteOpenConfigurationFile, (object parameter) => true, NuGetUIThreadHelper.JoinableTaskFactory);
            DataContext = this;
            InitializeComponent();
        }

        private void ExecuteOpenConfigurationFile(object obj)
        {
            SelectedPath = (ConfigPathsViewModel)_configurationPaths.SelectedItem;
            var componentModel = NuGetUIThreadHelper.JoinableTaskFactory.Run(ServiceLocator.GetComponentModelAsync);
            var projectContext = componentModel.GetService<INuGetProjectContext>();
            _ = projectContext.ExecutionContext.OpenFile(SelectedPath.ConfigPath);
        }

        private void ListViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ExecuteOpenConfigurationFile(sender);
        }

        internal void InitializeOnActivated(CancellationToken cancellationToken)
        {
            ConfigPathsWindow.ConfigPaths.Clear();
            ConfigPathsWindow.SetConfigPaths();
        }

        public ICommand OpenConfigurationFile { get; set; }
    }
}
