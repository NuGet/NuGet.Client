using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// The DataContext of this control is DetailControlModel
    /// </summary>
    public partial class OptionsControl : UserControl
    {
        public OptionsControl()
        {
            InitializeComponent();
            SetStyles();
        }

        private void SetStyles()
        {
            if (StandaloneSwitch.IsRunningStandalone)
            {
                return;
            }

            _dependencyBehaviors.Style = Styles.ThemedComboStyle;
            _fileConflictActions.Style = Styles.ThemedComboStyle;
        }

        private void ExecuteOpenExternalLink(object sender, ExecutedRoutedEventArgs e)
        {
            Hyperlink hyperlink = e.OriginalSource as Hyperlink;
            if (hyperlink != null && hyperlink.NavigateUri != null)
            {
                UIUtility.LaunchExternalLink(hyperlink.NavigateUri);
                e.Handled = true;
            }
        }
    }
}
