using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.VisualStudio.Shell;

namespace NuGet.PackageManagement.UI
{
    public static class Brushes
    {
        public static object HeaderBackground
        {
            get
            {
                return VsBrushes.BrandedUIBackgroundKey;
            }
        }

        public static object BorderBrush
        {
            get
            {
                return VsBrushes.BrandedUIBorderKey;
            }
        }

        public static object ListPaneBackground
        {
            get
            {
                return VsBrushes.BrandedUIBackgroundKey;
            }
        }

        public static object DetailPaneBackground
        {
            get
            {
                return VsBrushes.BrandedUIBackgroundKey;
            }
        }

        public static object LegalMessageBackground
        {
            get
            {
                return VsBrushes.BrandedUIBackgroundKey;
            }
        }

        public static object UIText
        {
            get
            {
                return VsBrushes.BrandedUITextKey;
            }
        }

        public static object ControlLinkTextKey
        {
            get
            {
                return Microsoft.VisualStudio.Shell.VsBrushes.ControlLinkTextKey;
            }
        }

        public static object ControlLinkTextHoverKey
        {
            get
            {
                return Microsoft.VisualStudio.Shell.VsBrushes.ControlLinkTextHoverKey;
            }
        }

        public static object WindowTextKey
        {
            get
            {
                return Microsoft.VisualStudio.Shell.VsBrushes.WindowTextKey;
            }
        }

        public static object IndicatorFillBrushKey
        {
            get
            {
                if (StandaloneSwitch.IsRunningStandalone)
                {
                    return System.Windows.SystemColors.WindowFrameColor;
                }
                else
                {
                    return Microsoft.VisualStudio.PlatformUI.ProgressBarColors.IndicatorFillBrushKey;
                }
            }
        }

        internal static void Initialize()
        {
            var assembly = AppDomain.CurrentDomain.Load("Microsoft.VisualStudio.ExtensionsExplorer.UI");
            var colorResources = assembly.GetType("Microsoft.VisualStudio.ExtensionsExplorer.UI.ColorResources");
            
            var prop = colorResources.GetProperty("ContentMouseOverBrushKey");
            _contentMouseOverBrushKey = prop.GetValue(null);

            prop = colorResources.GetProperty("ContentInactiveSelectedBrushKey");
            _contentInactiveSelectedBrushKey = prop.GetValue(null);

            prop = colorResources.GetProperty("ContentSelectedBrushKey");
            _contentSelectedBrushKey = prop.GetValue(null);

            prop = colorResources.GetProperty("ContentSelectedTextBrushKey");
            _contentSelectedTextBrushKey = prop.GetValue(null);
        }

        private static object _contentMouseOverBrushKey;

        public static object ContentMouseOverBrushKey
        {
            get
            {
                return _contentMouseOverBrushKey;
            }
        }

        private static object _contentInactiveSelectedBrushKey;

        public static object ContentInactiveSelectedBrushKey
        {
            get
            {
                return _contentInactiveSelectedBrushKey;
            }
        }

        private static object _contentSelectedBrushKey;

        public static object ContentSelectedBrushKey
        {
            get
            {
                return _contentSelectedBrushKey;
            }
        }

        private static object _contentSelectedTextBrushKey;

        public static object ContentSelectedTextBrushKey
        {
            get
            {
                return _contentSelectedTextBrushKey;
            }
        }
    }

    public static class Styles
    {
        public static void Initialize()
        {
            var assembly = AppDomain.CurrentDomain.Load("Microsoft.VisualStudio.ExtensionsExplorer.UI");
            var comboBoxType = assembly.GetType("Microsoft.VisualStudio.ExtensionsExplorer.UI.AutomationComboBox");
            _themedComboStyle = Application.Current.FindResource(
                new ComponentResourceKey(comboBoxType, "ThemedComboBoxStyle")) as Style;
        }

        private static Style _themedComboStyle;

        public static Style ThemedComboStyle 
        {
            get
            {
                return _themedComboStyle;
            }
        }
    }
}
