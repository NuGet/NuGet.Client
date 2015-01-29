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
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class LicenseAcceptanceWindow : VsDialogWindow
    {
        public LicenseAcceptanceWindow()
        {
            InitializeComponent();
        }

        private void OnViewLicenseTermsRequestNavigate(object sender, RoutedEventArgs e)
        {
            Hyperlink hyperlink = (Hyperlink)sender;
            if (hyperlink != null && hyperlink.NavigateUri != null)
            {
                UIUtility.LaunchExternalLink(hyperlink.NavigateUri);
                e.Handled = true;
            }
        }

        private void OnDeclineButtonClick(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void OnAcceptButtonClick(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
    }
}
