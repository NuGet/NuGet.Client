using System.Windows.Controls;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell.Interop;

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
