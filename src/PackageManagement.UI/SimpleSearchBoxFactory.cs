using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace NuGet.PackageManagement.UI
{
    public class SimpleSearchBoxFactory : IVsWindowSearchHostFactory
    {
        public IVsWindowSearchHost CreateWindowSearchHost(object pParentControl, IDropTarget pDropTarget = null)
        {
            var parent = pParentControl as Border;
            
            var box = new SimpleSearchBox();
            

            if (parent != null)
            {
                parent.Child = box;
            }

            return box;
        }
    }
}
