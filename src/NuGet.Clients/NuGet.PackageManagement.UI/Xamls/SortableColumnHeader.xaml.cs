// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Interaction logic for SortableColumnHeader.xaml. If a column is sortable in 
    /// the project list view, then the column header of that column is this control.
    /// </summary>
    public partial class SortableColumnHeader : UserControl
    {
        public SortableColumnHeader()
        {
            InitializeComponent();
        }

        /// <summary>
        /// The text displayed on the header.
        /// </summary>
        public string HeaderText
        {
            get
            {
                return _headerText.Text;
            }
            set
            {
                _headerText.Text = value;
            }
        }

        private ListSortDirection? _sortDirection;

        /// <summary>
        /// The current sort direction of the column. null means the column 
        /// is not sorted.
        /// </summary>
        public ListSortDirection? SortDirection
        {
            get
            {
                return _sortDirection;
            }
            set
            {
                _sortDirection = value;

                // update the sort direction indicator
                if (_sortDirection == null)
                {
                    _downArrow.Visibility = Visibility.Hidden;
                    _upArrow.Visibility = Visibility.Hidden;
                }
                else if (_sortDirection == ListSortDirection.Ascending)
                {
                    _downArrow.Visibility = Visibility.Hidden;
                    _upArrow.Visibility = Visibility.Visible;
                }
                else
                {
                    _downArrow.Visibility = Visibility.Visible;
                    _upArrow.Visibility = Visibility.Hidden;
                }
            }
        }

        /// <summary>
        /// The name of the property by which to sort.
        /// </summary>
        public string SortPropertyName
        {
            get; set;
        }
    }
}