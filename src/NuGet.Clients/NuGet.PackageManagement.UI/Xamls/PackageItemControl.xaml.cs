// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// This control is used as list items in the package list. Its DataContext is
    /// PackageItemListViewModel.
    /// </summary>
    public partial class PackageItemControl : UserControl
    {
        public PackageItemControl()
        {
            InitializeComponent();
        }

        // Whenever the checkbox in the item is checked, the containing item will raise a 
        // UIA event to convey the fact that the toggle state of the item has changed. The 
        // event must be raised regardless of whether the toggle state of the item changed 
        // in response to keyboard, mouse, or programmatic input.
        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            var itemCheckBox = sender as CheckBox;
            if (itemCheckBox is null || itemCheckBox.Visibility != Visibility.Visible)
            {
                return;
            }

            var parentContentPresenter = VisualTreeHelper.GetParent(this) as ContentPresenter;
            if (parentContentPresenter is null)
            {
                return;
            }

            var parentBorder = VisualTreeHelper.GetParent(parentContentPresenter) as Border;
            if (parentBorder is null)
            {
                return;
            }

            var itemContainer = VisualTreeHelper.GetParent(parentBorder) as ListBoxItem;
            if (itemContainer is null)
            {
                return;
            }

            var isChecked = (e.RoutedEvent == CheckBox.CheckedEvent);

            AutomationPeer itemAutomationPeer = UIElementAutomationPeer.FromElement(itemContainer);
            if (itemAutomationPeer != null)
            {
                itemAutomationPeer.RaisePropertyChangedEvent(
                    TogglePatternIdentifiers.ToggleStateProperty,
                    isChecked ? ToggleState.Off : ToggleState.On, // Assume the state has actually toggled.
                    isChecked ? ToggleState.On : ToggleState.Off);
            }
        }
    }
}
