using System.Collections;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.VisualStudio.ComponentModelHost;
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
        public ObservableCollection<string> ConfigPaths { get; private set; }

        public ConfigPathsControl()
        {
            ConfigPaths = new ObservableCollection<string>();
            DataContext = this;
            InitializeComponent();
        }

        internal void InitializeOnActivated(CancellationToken cancellationToken)
        {
            // should caclculate the config files and create the view models
            // array of view models
            // each view model should represent a config file
            IComponentModel componentModelMapping = NuGetUIThreadHelper.JoinableTaskFactory.Run(ServiceLocator.GetComponentModelAsync);
            var settings = componentModelMapping.GetService<Configuration.ISettings>();
            var configPaths = settings.GetConfigFilePaths().ToList();
            ConfigPaths.AddRange(configPaths);

            // ObservableCollection<string> a = settings.GetConfigFilePaths().ToList();
            //ConfigPaths = settings.GetConfigFilePaths().OfType<ObservableCollection<string>();
        }

        public ICommand ShowAddDialogCommand { get; set; }
    }
}
