using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Interaction logic for InstallButton.xaml
    /// </summary>
    public partial class InstallButton : Button
    {
        // Brush used when mouse hovers over this control
        private static Brush _activeBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x1A, 0xA1, 0xE2));

        public InstallButton()
        {
            InitializeComponent();
            _icon.Brush = System.Windows.Media.Brushes.Black;
        }

        private void Button_MouseEnter(object sender, MouseEventArgs e)
        {
            _icon.Brush = _activeBrush;
        }

        private void Button_MouseLeave(object sender, MouseEventArgs e)
        {
            _icon.Brush = System.Windows.Media.Brushes.Black;
        }
    }
}