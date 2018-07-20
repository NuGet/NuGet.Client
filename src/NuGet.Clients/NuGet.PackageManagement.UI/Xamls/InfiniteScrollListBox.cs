using System.Threading;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using Microsoft.VisualStudio.Threading;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.UI
{ 
    internal class InfiniteScrollListBox : ListBox
    {
        public ReentrantSemaphore ItemsLock { get; set; }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new InfiniteScrollListBoxAutomationPeer(this);
        }
    }
}