using System.Windows;
using System.Windows.Controls;

namespace NuGet.PackageManagement.UI
{
    internal partial class SharedResources : ResourceDictionary
    {
        public SharedResources()
        {
            InitializeComponent();
        }

        private void PackageIconImage_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            var image = sender as Image;
            image.Source = Images.DefaultPackageIcon;
        }
    }
}