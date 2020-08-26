// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Represents Button controls which are used as Clickable Tab Items.
    /// Example: Browse, Installed, Update Tab items in the Nuget Package Manager tab
    /// </summary>
    internal class TabItemButton : Button
    {
        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new TabItemButtonAutomationPeer(this);
        }

        public void Select()
        {
            OnClick();
        }
    }
}
