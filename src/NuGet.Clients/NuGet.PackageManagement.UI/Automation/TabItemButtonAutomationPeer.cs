// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Automation peer to Represents Button controls which are used as Clickable Tab Items.
    /// This changes the ControlType for such buttons to TabItems and provides the necessary
    /// AutomationPatterns suitable for a tab item. 
    /// Example: Browse, Installed, Update Tab items in the Nuget Package Manager tab
    /// </summary>
    internal class TabItemButtonAutomationPeer : FrameworkElementAutomationPeer, ISelectionItemProvider
    {
        private readonly TabItemButton _tabItemButton;

        public TabItemButtonAutomationPeer(TabItemButton owner) : base(owner)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            _tabItemButton = owner;
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
                return _tabItemButton.IsFocused;
            }
        }

        public IRawElementProviderSimple SelectionContainer
        {
            get
            {
                return ProviderFromPeer(this);
            }
        }

        public void Select()
        {
            _tabItemButton.Select();
        }

        public void AddToSelection()
        {
            _tabItemButton.Select();
        }

        public void RemoveFromSelection() { /* No-op */ }

        #endregion
    }
}
