using Microsoft.VisualStudio.PlatformUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace NuGet.PackageManagement.UI
{

#if STANDALONE

    public class VsDialogWindow : Window
    {
        public VsDialogWindow()
        {

        }

        public bool? ShowModal()
        {
            return this.ShowDialog();
        }
    }

#else
    public class VsDialogWindow : DialogWindow
    {
        // Wrapper for the VS dialog
    }
#endif
}
