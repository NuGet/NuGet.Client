// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

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

            // Change ItemContainerStyle of the _versions combobox so that
            // for a null value, a separator is generated.
            var dataTrigger = new DataTrigger();
            dataTrigger.Binding = new Binding();
            dataTrigger.Value = null;
            dataTrigger.Setters.Add(new Setter(TemplateProperty, FindResource("SeparatorControlTemplate")));

            // make sure the separator can't be selected thru keyboard navigation.
            dataTrigger.Setters.Add(new Setter(IsEnabledProperty, false));

            var style = new Style(typeof(ComboBoxItem), _versions.ItemContainerStyle);
            style.Triggers.Add(dataTrigger);
            _versions.ItemContainerStyle = style;

            _projectList.SizeChanged += ListView_SizeChanged;

            _sortableColumns = new List<GridViewColumnHeader>
            {
                _projectColumnHeader,
                _versionColumnHeader
            };

            SortByColumn(_projectColumnHeader);
        }

        private void UninstallButton_Clicked(object sender, RoutedEventArgs e)
        {
            UninstallButtonClicked?.Invoke(this, EventArgs.Empty);
        }

        private void InstallButton_Clicked(object sender, RoutedEventArgs e)
        {
            InstallButtonClicked?.Invoke(this, EventArgs.Empty);
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
            if (sortDescription != default(SortDescription))
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
                    return StringComparer.OrdinalIgnoreCase.Equals(
                        SortableColumnHeaderAttachedProperties.GetSortPropertyName(obj: column),
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
            SortableColumnHeaderAttachedProperties.SetSortDirectionProperty(obj: sortColumn, value: userSettings.SortDirection);

            // clear sort direction of other columns
            foreach (var column in _sortableColumns)
            {
                if (column == sortColumn)
                {
                    continue;
                }

                SortableColumnHeaderAttachedProperties.RemoveSortDirectionProperty(obj: column);
            }
        }
        
        private void SortByColumn(GridViewColumnHeader sortColumn)
        {
            _projectList.Items.SortDescriptions.Clear();

            var sortDescription = new SortDescription();

            sortDescription.PropertyName = SortableColumnHeaderAttachedProperties.GetSortPropertyName(sortColumn);
            var sortDir = SortableColumnHeaderAttachedProperties.GetSortDirectionProperty(sortColumn);

            sortDescription.Direction = sortDir == null
                ? ListSortDirection.Ascending
                : sortDir == ListSortDirection.Ascending
                    ? ListSortDirection.Descending
                    : ListSortDirection.Ascending;

            SortableColumnHeaderAttachedProperties.SetSortDirectionProperty(obj: sortColumn, value: sortDescription.Direction);

            _projectList.Items.SortDescriptions.Add(sortDescription);

            foreach (var column in _sortableColumns)
            {
                if (column == sortColumn)
                {
                    continue;
                }

                SortableColumnHeaderAttachedProperties.RemoveSortDirectionProperty(obj: column);
            }
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            var model = DataContext as PackageSolutionDetailControlModel;
            model?.SelectAllProjects();
        }

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            var model = DataContext as PackageSolutionDetailControlModel;
            model?.UnselectAllProjects();
        }

        private void ListView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // adjust the width of the "project" column so that it takes 
            // up all remaining width.
            var gridView = (GridView)_projectList.View;
            var width = _projectList.ActualWidth - 2 * SystemParameters.VerticalScrollBarWidth;
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

        private void SortableColumnHeader_SizeChanged(object sender, SizeChangedEventArgs sizeChangedEventArgs)
        {
            // GridViewColumnHeader doesn't handle setting minwidth very well so we prevent it here.
            if (sizeChangedEventArgs.NewSize.Width <= 60)
            {
                sizeChangedEventArgs.Handled = true;
                ((GridViewColumnHeader)sender).Column.Width = 60;
            }
        }

        private void ProjectList_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            // toggle the selection state when user presses the space bar when focus is on the ListViewItem
            var packageInstallationInfo = _projectList.SelectedItem as PackageInstallationInfo;
            if (packageInstallationInfo != null
                && e.Key == Key.Space
                && ((ListViewItem)(_projectList.ItemContainerGenerator.ContainerFromItem(_projectList.SelectedItem))).IsFocused)
            {
                packageInstallationInfo.IsSelected = !packageInstallationInfo.IsSelected;
                e.Handled = true;
            }
        }

        private void SortableColumnHeader_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            var sortableColumnHeader = sender as GridViewColumnHeader;
            if(sortableColumnHeader != null && (e.Key == Key.Space || e.Key == Key.Enter))
            {
                SortByColumn(sortableColumnHeader);
                e.Handled = true;
            }
        }
    }
}