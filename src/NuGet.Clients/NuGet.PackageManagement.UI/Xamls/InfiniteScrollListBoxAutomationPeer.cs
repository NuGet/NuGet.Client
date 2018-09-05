using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.UI
{
    internal class InfiniteScrollListBoxAutomationPeer : ListBoxAutomationPeer
    {
        public InfiniteScrollListBoxAutomationPeer(ListBox owner) : base(owner) { }

        protected override List<AutomationPeer> GetChildrenCore()
        {
            var infiniteScrollListBox = Owner as InfiniteScrollListBox;

            if (infiniteScrollListBox == null)
            {
                return new List<AutomationPeer>();
            }

            List<AutomationPeer> value = null;

            NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await infiniteScrollListBox.ItemsLock.ExecuteAsync(() =>
                {
                    // Don't return the LoadingStatusIndicator as an AutomationPeer, otherwise narrator will report it as an item in the list of packages, even when not visible
                    value = base.GetChildrenCore()?.Where(lbiap => !(((ListBoxItemAutomationPeer)lbiap).Item is LoadingStatusIndicator)).ToList() ?? new List<AutomationPeer>();

                    return Task.CompletedTask;
                });
            });

            return value;
        }
    }
}