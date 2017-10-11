using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace NuGet.PackageManagement.UI
{ 
    internal class InfiniteScrollListBox : ListBox
    {
        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new InfiniteScrollListBoxAutomationPeer(this);
        }
    }
}