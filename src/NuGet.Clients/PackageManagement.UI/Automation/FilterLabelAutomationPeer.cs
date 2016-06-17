using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace NuGet.PackageManagement.UI.Automation
{
    public class FilterLabelAutomationPeer : ButtonAutomationPeer
    {
        private FilterLabel _filterLabel;
        public FilterLabelAutomationPeer(Button button, FilterLabel owner) : base(button)
        {
            _filterLabel = owner;
        }

        protected override string GetNameCore()
        {
            return _filterLabel.Text;
        }

        protected override string GetClassNameCore()
        {
            return _filterLabel.GetType().Name;
        }
    }
}
