using System.Threading;
using System.Windows.Controls;
using System.Windows.Input;

namespace NuGet.PackageManagement.UI.Options
{
    /// <summary>
    /// Interaction logic for ConfigPathsControl.xaml
    /// </summary>
    public partial class ConfigPathsControl : UserControl
    {
        //private AddMappingDialog _addMappingDialog;

        public ConfigPathsControl()
        {
            DataContext = this;
            InitializeComponent();
        }

        internal void InitializeOnActivated(CancellationToken cancellationToken)
        {
        }

        public ICommand ShowAddDialogCommand { get; set; }
    }
}
