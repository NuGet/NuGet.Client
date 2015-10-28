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
                    // Consolidate tab is only available in solution package manager
                    _labelConsolidate.Visibility = Visibility.Collapsed;

                    // if consolidate tab is currently selected, we need to select another
                    // tab.
                    if (_selectedFilter == _labelConsolidate)
                    {
                        SelectFilter(Filter.Installed);
                    }
                }
                else
                {
                    _labelConsolidate.Visibility = Visibility.Visible;
                }
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

                case Filter.Consolidate:
                    if (_isSolution)
                    {
                        _selectedFilter = _labelConsolidate;
                    }
                    break;
            }

            // _selectedFilter could be null if we are running with a solution with user
            // settings saved by a later version of NuGet that has more filters than
            // can be recognized here.
            if (_selectedFilter == null)
            {
                _selectedFilter = _labelInstalled;
            }

            _selectedFilter.Selected = true;
        }
    }
}