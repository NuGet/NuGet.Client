// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft;
using Microsoft.VisualStudio.PlatformUI;
using Resx = NuGet.PackageManagement.UI.Resources;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// The panel which is located at the top of the package manager window.
    /// </summary>
    public partial class PackageManagerTopPanel : UserControl
    {
        private TabItem SelectedTabItem
        {
            get
            {
                return tabsPackageManagement.SelectedItem as TabItem;
            }
            set
            {
                tabsPackageManagement.SelectedItem = value;
            }
        }
        public TabItem TabConsolidate { get; private set; }
        public Border CountConsolidateContainer { get; private set; }
        public TextBlock CountConsolidate { get; private set; }

        public PackageManagerTopPanel()
        {
            InitializeComponent();
        }

        public void CreateAndAddConsolidateTab()
        {
            var tabConsolidate = new TabItem();
            tabConsolidate.Name = nameof(tabConsolidate);
            tabConsolidate.Tag = ItemFilter.Consolidate;

            var sp = new StackPanel()
            {
                Orientation = Orientation.Horizontal
            };

            var textConsolidate = new TextBlock();
            textConsolidate.Name = nameof(textConsolidate);
            textConsolidate.Text = Resx.Action_Consolidate;
            sp.Children.Add(textConsolidate);

            SetConsolidationAutomationProperties(tabConsolidate, count: 0);

            //The textblock that displays the count.
            CountConsolidateContainer = new Border()
            {
                Name = nameof(CountConsolidateContainer),
                CornerRadius = new CornerRadius(uniformRadius: 2),
                Margin = new Thickness(left: 3, top: 0, right: 3, bottom: 0),
                Padding = new Thickness(left: 3, top: 0, right: 3, bottom: 0),
                Visibility = Visibility.Collapsed,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            CountConsolidateContainer.SetResourceReference(dp: Border.BackgroundProperty, name: Brushes.TabPopupBrushKey);
            sp.Children.Add(element: CountConsolidateContainer);

            CountConsolidate = new TextBlock()
            {
                Name = nameof(CountConsolidate),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
            };
            CountConsolidate.SetResourceReference(dp: TextBlock.ForegroundProperty, name: Brushes.TabPopupTextBrushKey);
            CountConsolidateContainer.Child = CountConsolidate;

            //FontSize needs to match the UserControl.
            var bindingFontSize = new Binding(path: nameof(TextBlock.FontSize));
            bindingFontSize.RelativeSource = new RelativeSource(mode: RelativeSourceMode.FindAncestor, ancestorType: typeof(UserControl), ancestorLevel: 1);
            BindingOperations.SetBinding(target: CountConsolidate, dp: TextBlock.FontSizeProperty, binding: bindingFontSize);

            tabConsolidate.Header = sp;
            TabConsolidate = tabConsolidate;
            tabsPackageManagement.Items.Add(tabConsolidate);
        }

        private void SetConsolidationAutomationProperties(TabItem tabConsolidate, int count)
        {
            string automationString = null;
            if (count > 0)
            {
                automationString = string.Format(
                    CultureInfo.CurrentCulture,
                    "{0}{1}",
                    Resx.Action_Consolidate,
                    count);
            }
            else
            {
                automationString = Resx.Action_Consolidate;
            }
            tabConsolidate.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, automationString);
        }

        public void UpdateDeprecationStatusOnInstalledTab(int installedDeprecatedPackagesCount)
        {
            bool hasInstalledDeprecatedPackages = installedDeprecatedPackagesCount > 0;
            if (hasInstalledDeprecatedPackages)
            {
                _warningIcon.Visibility = Visibility.Visible;
                _warningIcon.ToolTip = string.Format(
                    CultureInfo.CurrentCulture,
                    NuGet.PackageManagement.UI.Resources.Label_Installed_DeprecatedWarning,
                    installedDeprecatedPackagesCount);
            }
            else
            {
                _warningIcon.Visibility = Visibility.Collapsed;
                _warningIcon.ToolTip = null;
            }
        }

        public void UpdateCountOnConsolidateTab(int count)
        {
            if (count > 0)
            {
                CountConsolidate.Text = count.ToString(CultureInfo.CurrentCulture);
                CountConsolidateContainer.Visibility = Visibility.Visible;
            }
            else
            {
                CountConsolidateContainer.Visibility = Visibility.Collapsed;
            }
            SetConsolidationAutomationProperties(TabConsolidate, count);
        }

        public void UpdateCountOnUpdatesTab(int count)
        {
            if (count > 0)
            {
                _countUpdates.Text = count.ToString(CultureInfo.CurrentCulture);
                _countUpdatesContainer.Visibility = Visibility.Visible;
            }
            else
            {
                _countUpdatesContainer.Visibility = Visibility.Collapsed;
            }
        }

        // the control that is used as container for the search box.
        public Border SearchControlParent => _searchControlParent;

        public CheckBox CheckboxPrerelease => _checkboxPrerelease;

        public ComboBox SourceRepoList => _sourceRepoList;

        public ToolTip SourceToolTip => _sourceTooltip;

        public ItemFilter Filter { get; private set; }

        public string Title
        {
            get { return _label.Text; }
            set { _label.Text = value; }
        }

        // Indicates if the control is hosted in solution package manager.
        private bool _isSolution;

        public bool IsSolution
        {
            get
            {
                return _isSolution;
            }
            set
            {
                if (_isSolution == value)
                {
                    return;
                }

                _isSolution = value;
                if (!_isSolution)
                {
                    // if consolidate tab is currently selected, we need to select another
                    // tab.
                    if (SelectedTabItem == TabConsolidate)
                    {
                        SelectFilter(ItemFilter.Installed);
                    }
                }
            }
        }

        private void _checkboxPrerelease_Checked(object sender, RoutedEventArgs e)
        {
            PrereleaseCheckChanged?.Invoke(this, EventArgs.Empty);
        }

        private void _checkboxPrerelease_Unchecked(object sender, RoutedEventArgs e)
        {
            PrereleaseCheckChanged?.Invoke(this, EventArgs.Empty);
        }

        private void _sourceRepoList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SourceRepoListSelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        private void _settingsButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsButtonClicked?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler<FilterChangedEventArgs> FilterChanged;

        public event EventHandler<EventArgs> SettingsButtonClicked;

        public event EventHandler<EventArgs> PrereleaseCheckChanged;

        public event EventHandler<EventArgs> SourceRepoListSelectionChanged;

        public void SelectFilter(ItemFilter selectedFilter)
        {
            switch (selectedFilter)
            {
                case ItemFilter.All:
                    SelectedTabItem = tabBrowse;
                    break;

                case ItemFilter.Installed:
                    SelectedTabItem = tabInstalled;
                    break;

                case ItemFilter.UpdatesAvailable:
                    SelectedTabItem = tabUpdates;
                    break;

                case ItemFilter.Consolidate:
                    if (_isSolution)
                    {
                        SelectedTabItem = TabConsolidate;
                    }
                    break;
            }

            // _selectedFilter could be null if we are running with a solution with user
            // settings saved by a later version of NuGet that has more filters than
            // can be recognized here.
            if (SelectedTabItem == null)
            {
                SelectedTabItem = tabInstalled;
            }
        }

        private void TabsPackageManagement_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            TabItem previousTabItem = e.RemovedItems.Count > 0 ? e.RemovedItems[0] as TabItem : null;
            TabItem selectedTabItem = e.AddedItems.Count > 0 ? e.AddedItems[0] as TabItem : null;

            //Store the tag for calculating the ItemFilter without having to access the UI Thread.
            Filter = GetItemFilter(selectedTabItem);

            if (previousTabItem != null)
            {
                if (FilterChanged != null)
                {
                    ItemFilter previousFilter = GetItemFilter(previousTabItem);
                    FilterChanged(this, new FilterChangedEventArgs(previousFilter));
                }
            }
        }

        private static ItemFilter GetItemFilter(TabItem item)
        {
            return (ItemFilter)item.Tag;
        }
    }

    public class FilterChangedEventArgs : EventArgs
    {
        public ItemFilter? PreviousFilter
        {
            get;
        }

        public FilterChangedEventArgs(ItemFilter? previousFilter)
        {
            PreviousFilter = previousFilter;
        }
    }
}
