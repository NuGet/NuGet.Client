// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Linq;
using NuGet.Versioning;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Interaction logic for SolutionView.xaml. The DataContext is PackageSolutionDetailControlModel.
    /// </summary>
    public partial class SolutionView : UserControl
    {
        public event EventHandler<EventArgs> InstallButtonClicked;

        public event EventHandler<EventArgs> UninstallButtonClicked;

        // the list of columns that are sortable.
        private List<GridViewColumnHeader> _sortableColumns;

        public SolutionView()
        {
            InitializeComponent();

            _projectList.SizeChanged += ListView_SizeChanged;

            _sortableColumns = new List<GridViewColumnHeader> {
                _projectColumnHeader,
                _versionColumnHeader
            };

            SortByColumn(_projectColumnHeader);
        }

        private void UninstallButton_Clicked(object sender, RoutedEventArgs e)
        {
            if (UninstallButtonClicked != null)
            {
                UninstallButtonClicked(this, EventArgs.Empty);
            }
        }

        private void InstallButton_Clicked(object sender, RoutedEventArgs e)
        {
            if (InstallButtonClicked != null)
            {
                InstallButtonClicked(this, EventArgs.Empty);
            }
        }

        private void ColumnHeader_Clicked(object sender, RoutedEventArgs e)
        {
            var columnHeader = sender as GridViewColumnHeader;
            if (columnHeader == null)
            {
                return;
            }

            SortByColumn(columnHeader);
        }

        public void SaveSettings(UserSettings settings)
        {
            var sortDescription = _projectList.Items.SortDescriptions.FirstOrDefault();
            if (sortDescription != null)
            {
                settings.SortPropertyName = sortDescription.PropertyName;
                settings.SortDirection = sortDescription.Direction;
            }
        }

        public void RestoreUserSettings(UserSettings userSettings)
        {
            // find the column to sort
            var sortColumn = _sortableColumns.FirstOrDefault(
                column =>
                {
                    var header = column.Content as SortableColumnHeader;
                    return StringComparer.OrdinalIgnoreCase.Equals(
                        header?.SortPropertyName,
                        userSettings.SortPropertyName);
                });
            if (sortColumn == null)
            {
                return;
            }

            // add new sort description
            _projectList.Items.SortDescriptions.Clear();
            _projectList.Items.SortDescriptions.Add(
                new SortDescription(
                    userSettings.SortPropertyName,
                    userSettings.SortDirection));

            // upate sortInfo
            var sortInfo = (SortableColumnHeader)sortColumn.Content;
            sortInfo.SortDirection = userSettings.SortDirection;

            // clear sort direction of other columns
            foreach (var column in _sortableColumns)
            {
                if (column == sortColumn)
                {
                    continue;
                }

                sortInfo = (SortableColumnHeader)column.Content;
                sortInfo.SortDirection = null;
            }
        }

        private void SortByColumn(GridViewColumnHeader sortColumn)
        {
            var sortInfo = sortColumn.Content as SortableColumnHeader;
            if (sortInfo == null)
            {
                return;
            }

            _projectList.Items.SortDescriptions.Clear();

            // add new sort description
            var sortDescription = new SortDescription();
            sortDescription.PropertyName = sortInfo.SortPropertyName;
            if (sortInfo.SortDirection == null)
            {
                sortDescription.Direction = ListSortDirection.Ascending;
            }
            else if (sortInfo.SortDirection == ListSortDirection.Ascending)
            {
                sortDescription.Direction = ListSortDirection.Descending;
            }
            else
            {
                sortDescription.Direction = ListSortDirection.Ascending;
            }

            _projectList.Items.SortDescriptions.Add(sortDescription);

            // upate sortInfo
            sortInfo.SortDirection = sortDescription.Direction;

            // clear sort direction of other columns
            foreach (var column in _sortableColumns)
            {
                if (column == sortColumn)
                {
                    continue;
                }

                sortInfo = (SortableColumnHeader)column.Content;
                sortInfo.SortDirection = null;
            }
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            var model = DataContext as PackageSolutionDetailControlModel;
            if (model == null)
            {
                return;
            }

            model.SelectAllProjects();
        }

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            var model = DataContext as PackageSolutionDetailControlModel;
            if (model == null)
            {
                return;
            }

            model.UnselectAllProjects();
        }

        private void ListView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // adjust the width of the "project" column so that it takes 
            // up all remaining width.
            var gridView = (GridView)_projectList.View;
            var width = _projectList.ActualWidth - SystemParameters.VerticalScrollBarWidth;
            foreach (var column in gridView.Columns)
            {
                var header = (GridViewColumnHeader)column.Header;
                width -= header.ActualWidth;
            }

            var newWidth = _projectColumnHeader.ActualWidth + width;
            _projectColumn.Width = newWidth;

            // this width adjustment is only done once.
            _projectList.SizeChanged -= ListView_SizeChanged;
        }
    }
}