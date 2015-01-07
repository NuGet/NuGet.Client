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
using System.Windows.Shapes;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Interaction logic for InstallPreviewWindow.xaml
    /// </summary>
    public partial class PreviewWindow : VsDialogWindow
    {
        public PreviewWindow()
        {
            InitializeComponent();
        }

        private void CancelButtonClicked(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void OkButtonClicked(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
