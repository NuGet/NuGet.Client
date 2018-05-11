using System.Threading;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace NuGet.PackageManagement.UI
{ 
    internal class InfiniteScrollListBox : ListBox
    {
        public readonly SemaphoreSlim ItemsLock = new SemaphoreSlim(1, 1);

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new InfiniteScrollListBoxAutomationPeer(this);
        }
    }
}