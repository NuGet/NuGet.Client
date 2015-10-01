// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// The panel which is located at the top of the package manager window.
    /// </summary>
    public partial class PackageManagerTopPanel : UserControl
    {
        private FilterLabel _selectedFilter;

        public PackageManagerTopPanel()
        {
            InitializeComponent();

            _labelBrowse.Selected = true;
            _selectedFilter = _labelBrowse;
        }

        // the control that is used as container for the search box.
        public Border SearchControlParent
        {
            get
            {
                return _searchControlParent;
            }
        }

        public CheckBox CheckboxPrerelease
        {
            get
            {
                return _checkboxPrerelease;
            }
        }

        public ComboBox SourceRepoList
        {
            get
            {
                return _sourceRepoList;
            }
        }

        public ToolTip SourceToolTip
        {
            get
            {
                return _sourceTooltip;
            }
        }

        public Filter Filter
        {
            get
            {
                return _selectedFilter.Filter;
            }
        }

        private void _checkboxPrerelease_Checked(object sender, RoutedEventArgs e)
        {
            if (PrereleaseCheckChanged != null)
            {
                PrereleaseCheckChanged(this, EventArgs.Empty);
            }
        }

        private void _checkboxPrerelease_Unchecked(object sender, RoutedEventArgs e)
        {
            if (PrereleaseCheckChanged != null)
            {
                PrereleaseCheckChanged(this, EventArgs.Empty);
            }
        }

        private void _sourceRepoList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SourceRepoListSelectionChanged != null)
            {
                SourceRepoListSelectionChanged(this, EventArgs.Empty);
            }
        }

        private void _settingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (SettingsButtonClicked != null)
            {
                SettingsButtonClicked(this, EventArgs.Empty);
            }
        }

        public void FilterLabel_ControlSelected(object sender, EventArgs e)
        {
            var selectedFilter = (FilterLabel)sender;
            if (selectedFilter == _selectedFilter)
            {
                return;
            }

            if (_selectedFilter != null)
            {
                _selectedFilter.Selected = false;
            }

            _selectedFilter = selectedFilter;
            if (FilterChanged != null)
            {
                FilterChanged(this, EventArgs.Empty);
            }
        }

        public event EventHandler<EventArgs> FilterChanged;

        public event EventHandler<EventArgs> SettingsButtonClicked;

        public event EventHandler<EventArgs> PrereleaseCheckChanged;

        public event EventHandler<EventArgs> SourceRepoListSelectionChanged;

        public void SelectFilter(Filter selectedFilter)
        {
            if (_selectedFilter != null)
            {
                _selectedFilter.Selected = false;
            }

            switch (selectedFilter)
            {
                case Filter.All:
                    _selectedFilter = _labelBrowse;
                    break;

                case Filter.Installed:
                    _selectedFilter = _labelInstalled;
                    break;

                case Filter.UpdatesAvailable:
                    _selectedFilter = _labelUpgradeAvailable;
                    break;
            }

            _selectedFilter.Selected = true;
        }
    }
}