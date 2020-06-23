using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Data;
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
                    List<AutomationPeer> items = base.GetChildrenCore();
                    if (items == null)
                    {
                        value = new List<AutomationPeer>();
                    }
                    else
                    {
                        var listCollectionView = (ListCollectionView)CollectionViewSource.GetDefaultView(items);
                        // Don't return the LoadingStatusIndicator as an AutomationPeer, otherwise narrator will report it as an item in the list of packages, even when not visible
                        IEnumerable<AutomationPeer> packageItemsInView = items.Where(item => ((ListBoxItemAutomationPeer)item).Item is PackageItemListViewModel
                                                                                             && listCollectionView.Contains(item));
                        value = packageItemsInView.ToList();
                    }

                    return Task.CompletedTask;
                });
            });

            return value;
        }
    }
}
