using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Interaction logic for PackageManagementFormatWindow.xaml
    /// </summary>
    public partial class PackageManagementFormatWindow : VsDialogWindow
    {
        private INuGetUIContext _uiContext;

        public PackageManagementFormatWindow(INuGetUIContext uiContext)
        {
            _uiContext = uiContext;
            InitializeComponent();
        }

        private void CancelButtonClicked(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void OkButtonClicked(object sender, RoutedEventArgs e)
        {
            var selectedFormat = DataContext as PackageManagementFormat;

            if (selectedFormat != null)
            {
                selectedFormat.ApplyChanges();
            }

            DialogResult = true;
        }

        private void ExecuteOpenExternalLink(object sender, ExecutedRoutedEventArgs e)
        {
            var hyperlink = e.OriginalSource as Hyperlink;
            if (hyperlink != null
                && hyperlink.NavigateUri != null)
            {
                UIUtility.LaunchExternalLink(hyperlink.NavigateUri);
                e.Handled = true;
            }
        }

        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            _uiContext.OptionsPageActivator.ActivatePage(OptionsPage.General, null);
        }

    }
}
