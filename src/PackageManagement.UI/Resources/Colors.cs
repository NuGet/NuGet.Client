using System;
using System.Collections.Generic;
using System.Linq;
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
    }

    public static class Styles
    {
        static Style _themedComboStyle = Application.Current.FindResource(
                new ComponentResourceKey(typeof(Microsoft.VisualStudio.ExtensionsExplorer.UI.AutomationComboBox), "ThemedComboBoxStyle")) as Style;

        public static Style ThemedComboStyle 
        {
            get
            {
                return _themedComboStyle;
            }
        }
    }
}
