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
    /// Interaction logic for ActionsAndVersions.xaml
    /// </summary>
    public partial class ActionsAndVersions : UserControl
    {
        public ActionsAndVersions()
        {
            InitializeComponent();
            SetStyles();

            // Change ItemContainerStyle of the _versions combobox so that 
            // for a null value, a separator is generated.
            var dataTrigger = new DataTrigger();
            dataTrigger.Binding = new Binding();
            dataTrigger.Value = null;
            dataTrigger.Setters.Add(new Setter(ComboBoxItem.TemplateProperty, this.FindResource("SeparatorControlTemplate")));

            // make sure the separator can't be selected thru keyboard navigation.
            dataTrigger.Setters.Add(new Setter(UIElement.IsEnabledProperty, false));

            var style = new Style(typeof(ComboBoxItem), _versions.ItemContainerStyle);
            style.Triggers.Add(dataTrigger);
            _versions.ItemContainerStyle = style;
        }

        private void SetStyles()
        {
            if (StandaloneSwitch.IsRunningStandalone)
            {
                return;
            }

            _actions.Style = Styles.ThemedComboStyle;
            _versions.Style = Styles.ThemedComboStyle;
        }
    }
}
