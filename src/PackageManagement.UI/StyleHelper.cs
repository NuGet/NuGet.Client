using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace NuGet.PackageManagement.UI
{
    public static class StyleHelper
    {

        public static object ComboBoxStyle
        {
            //   {StaticResource {ComponentResourceKey TypeInTargetAssembly={x:Type tp:AutomationComboBox}, ResourceId=ThemedComboBoxStyle}}
            get
            {
                //var component = new ComponentResourceKey(typeof(Microsoft.VisualStudio.ExtensionsExplorer.UI.AutomationComboBox), "theme");
                //System.Windows.Application.FindResource(component);


                //Microsoft.VisualStudio.ExtensionsExplorer.UI.AutomationComboBox

                return null;
            }
        }

    }
}
