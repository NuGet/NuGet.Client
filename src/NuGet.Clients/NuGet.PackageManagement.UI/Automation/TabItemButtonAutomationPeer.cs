// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace NuGet.PackageManagement.UI.Automation
{
    public class TabItemButtonAutomationPeer : FrameworkElementAutomationPeer, ISelectionItemProvider
    {
        private TabItemButton tabItemButton;

        public TabItemButtonAutomationPeer(TabItemButton owner) : base(owner)
        {
            this.tabItemButton = owner;
        }

        protected override AutomationControlType GetAutomationControlTypeCore()
        {
            return AutomationControlType.TabItem;
        }

        public override object GetPattern(PatternInterface patternInterface)
        {
            if (patternInterface == PatternInterface.SelectionItem)
            {
                return this;
            }
            return base.GetPattern(patternInterface);
        }

        #region ISelectionItemProvider Implementation
        public bool IsSelected
        {
            get
            {
                return this.tabItemButton.IsFocused;
            }
        }

        public IRawElementProviderSimple SelectionContainer
        {
            get
            {
                return this.ProviderFromPeer(this);
            }
        }

        public void Select()
        {
            this.tabItemButton.Select();
        }

        public void AddToSelection()
        {
            this.tabItemButton.Select();
        }

        public void RemoveFromSelection() { /* No-op */ }

        #endregion
    }
}
