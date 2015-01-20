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
    /// Interaction logic for ProjectList.xaml
    /// </summary>
    public partial class ProjectList : UserControl
    {
        public ProjectList()
        {
            InitializeComponent();
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            var model = (PackageSolutionDetailControlModel)DataContext;
            if (model == null)
            {
                return;
            }

            model.CheckAllProjects();
        }

        private void _checkBox_Unchecked(object sender, RoutedEventArgs e)
        {
            var model = (PackageSolutionDetailControlModel)DataContext;
            if (model == null)
            {
                return;
            }

            model.UncheckAllProjects();
        }
    }
}