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
    /// Interaction logic for UpdateButton.xaml
    /// </summary>
    public partial class UpdateButton : Button
    {
        // Brush used when mouse hovers over this control
        private static Brush _activeBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x1A, 0xA1, 0xE2));

        public UpdateButton()
        {
            InitializeComponent();
            _icon.Brush = System.Windows.Media.Brushes.Black;
        }

        private void UserControl_MouseEnter(object sender, MouseEventArgs e)
        {
            _icon.Brush = _activeBrush;
        }

        private void UserControl_MouseLeave(object sender, MouseEventArgs e)
        {
            _icon.Brush = System.Windows.Media.Brushes.Black;
        }
    }
}
