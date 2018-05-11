using System.Collections.Generic;
using System.Linq;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace NuGet.PackageManagement.UI
{
    internal class InfiniteScrollListBoxAutomationPeer : ListBoxAutomationPeer
    {
        public InfiniteScrollListBoxAutomationPeer(ListBox owner) : base(owner) { }

        protected override List<AutomationPeer> GetChildrenCore()
        {
            var infiniteScrollListBox = Owner as InfiniteScrollListBox;

            try
            {
                infiniteScrollListBox?.ItemsLock.Wait();

                // Don't return the LoadingStatusIndicator as an AutomationPeer, otherwise narrator will report it as an item in the list of packages, even when not visible
                return base.GetChildrenCore()?.Where(lbiap => !(((ListBoxItemAutomationPeer)lbiap).Item is LoadingStatusIndicator)).ToList() ?? new List<AutomationPeer>();
            }
            finally
            {
                infiniteScrollListBox?.ItemsLock.Release();
            }
        }
    }
}