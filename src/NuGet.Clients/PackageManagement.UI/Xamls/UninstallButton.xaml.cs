using System.Windows.Controls;
using System.Windows.Media;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Interaction logic for Cancel.xaml
    /// </summary>
    public partial class UninstallButton : Button
    {
        // Brush used when mouse hovers over this control
        private static Brush _activeBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xA1, 0x25, 0x0C));

        public UninstallButton()
        {
            InitializeComponent();
            _icon.Brush = System.Windows.Media.Brushes.Black;
        }

        private void UserControl_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _icon.Brush = _activeBrush;
        }

        private void UserControl_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _icon.Brush = System.Windows.Media.Brushes.Black;
        }
    }
}