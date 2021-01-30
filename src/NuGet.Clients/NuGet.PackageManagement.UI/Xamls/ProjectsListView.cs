// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace NuGet.PackageManagement.UI
{
    public class ProjectsListView : ListView, ISelectableItemsControl
    {
        public bool IsItemSelectionEnabled
        {
            get => true;

            set => throw new System.NotImplementedException();
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new ListBoxToggleableItemsAutomationPeer(this);
        }
    }
}
