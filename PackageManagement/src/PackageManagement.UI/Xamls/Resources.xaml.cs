using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.VisualStudio.Shell;

namespace NuGet.PackageManagement.UI
{
    public partial class NuGetResourceDictionary : ResourceDictionary
    {
        public NuGetResourceDictionary()
        {
            InitializeComponent();

            if (StandaloneSwitch.IsRunningStandalone)
            {
                return;
            }

            // when the UI is running inside Visual Studio, add the styles from Visual Studio
            // so that controls in the UI will use the same styles.

            // style for combobox
            var style = new Style(typeof(ComboBox), Styles.ThemedComboStyle);
            this.Add(typeof(ComboBox), style);

            // style for scroll bar
            style = new Style(typeof(ScrollBar), Styles.ScrollBarStyle);
            this.Add(typeof(ScrollBar), style);

            // style for scroll viewer
            style = new Style(typeof(ScrollViewer), Styles.ScrollViewerStyle);
            this.Add(typeof(ScrollViewer), style);
        }
    }
}