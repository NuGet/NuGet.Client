using NuGet.ProjectManagement;
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
    /// Interaction logic for FileConflictDialog.xaml
    /// </summary>
    public partial class FileConflictDialog : VsDialogWindow
    {
        public FileConflictDialog()
        {
            InitializeComponent();
        }

        public string Question
        {
            get
            {
                return QuestionText.Text;
            }
            set
            {
                QuestionText.Text = value;
            }
        }

        public FileConflictAction UserSelection
        {
            get;
            private set;
        }


        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            string tagValue = (string)button.Tag;

            UserSelection = (FileConflictAction)Enum.Parse(typeof(FileConflictAction), tagValue);

            DialogResult = true;
        }
    }
}
