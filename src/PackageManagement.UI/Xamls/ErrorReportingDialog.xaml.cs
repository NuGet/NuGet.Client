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
    /// Interaction logic for ErrorReportingDialog.xaml
    /// </summary>
    public partial class ErrorReportingDialog : VsDialogWindow
    {
        public ErrorReportingDialog(string message, string detail)
        {
            InitializeComponent();

            _message.Text = message;
            _detail.Text = detail;
        }

        private void Button_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void Expander_Collapsed(object sender, System.Windows.RoutedEventArgs e)
        {
            this.Height -= _detailPart.Height + _detailPart.Margin.Top + _detailPart.Margin.Bottom;
        }

        private void Expander_Expanded(object sender, System.Windows.RoutedEventArgs e)
        {
            this.Height += _detailPart.Height + _detailPart.Margin.Top + _detailPart.Margin.Bottom;
        }
    }
}
