using System.Threading;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.VisualStudio.PlatformUI;
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
        public ICommand OpenConfigurationFile { get; set; }

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
            ConfigPathsWindow.OpenConfigFile(SelectedPath);
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
    }
}
