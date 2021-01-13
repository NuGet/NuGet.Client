// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace NuGet.PackageManagement.UI
{
    internal class ToggleableItemAutomationPeer : ListBoxItemAutomationPeer, IToggleProvider
    {
        private readonly InfiniteScrollListBox _ownerParent;
        private readonly PackageItemListViewModel _owner;

        public ToggleableItemAutomationPeer(object item, SelectorAutomationPeer selectorAutomationPeer)
            : base(item, selectorAutomationPeer)
        {
            _owner = item as PackageItemListViewModel;
            _ownerParent = selectorAutomationPeer.Owner as InfiniteScrollListBox;
        }

        public override object GetPattern(PatternInterface patternInterface)
        {
            if (patternInterface == PatternInterface.Toggle && _ownerParent?.CheckboxesEnabled == true)
            {
                return this;
            }

            return base.GetPattern(patternInterface);
        }

        /// <summary>
        /// The default will be assumed to be toggled off (eg, Checked is false), therefore translating to `ToggleState.Off`.
        /// </summary>
        public ToggleState ToggleState
        {
            get
            {
                return _owner?.Selected == true ? ToggleState.On : ToggleState.Off;
            }
            set
            {
                if (_owner != null)
                {
                    _owner.Selected = value == ToggleState.On;
                }
            }
        }

        public void Toggle()
        {
            if (_owner != null)
            {
                _owner.Selected = !_owner.Selected;
            }
        }
    }
}
