using System.Collections;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.VisualStudio.Services.Common;

namespace NuGet.PackageManagement.UI.Options
{
    /// <summary>
    /// Interaction logic for ConfigPathsControl.xaml
    /// </summary>
    public partial class ConfigPathsControl : UserControl
    {
        //private AddMappingDialog _addMappingDialog;
        public System.Collections.Generic.IEnumerable<string> ConfigPaths { get; set; }

        public ConfigPathsControl()
        {
            var list = new System.Collections.Generic.List<string>();
            list.Add("1");
            list.Add("2");
            ConfigPaths = list;
            DataContext = this;
            InitializeComponent();
        }

        internal void InitializeOnActivated(CancellationToken cancellationToken)
        {
            // should caclculate the config files and create the view models
            // array of view models
            // each view model should represent a config file
        }

        public ICommand ShowAddDialogCommand { get; set; }
    }
}
