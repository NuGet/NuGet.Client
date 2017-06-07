// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Windows.Automation.Peers;
using System.Windows.Controls;
using NuGet.PackageManagement.UI.Automation;

namespace NuGet.PackageManagement.UI
{
    public class TabItemButton: Button
    {
        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new TabItemButtonAutomationPeer(this);
        }

        public void Select()
        {
            this.OnClick();
        }
    }
}
