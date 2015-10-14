// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Represents the filter label. E.g. Browse, Installed, Update Available.
    /// </summary>
    public partial class FilterLabel : UserControl
    {
        public FilterLabel()
        {
            InitializeComponent();
        }

        public event EventHandler<EventArgs> ControlSelected;

        public Filter Filter
        {
            get;
            set;
        }

        public string Text
        {
            get
            {
                return _labelText.Text;
            }
            set
            {
                _labelText.Text = value;
            }
        }

        private bool _selected;

        public bool Selected
        {
            get
            {
                return _selected;
            }
            set
            {
                _selected = value;
                if (_selected)
                {
                    _labelText.SetResourceReference(
                        TextBlock.ForegroundProperty,
                        Brushes.ContentSelectedBrushKey);
                    _underline.Visibility = Visibility.Visible;

                    if (ControlSelected != null)
                    {
                        ControlSelected(this, EventArgs.Empty);
                    }
                }
                else
                {
                    _labelText.SetResourceReference(
                        TextBlock.ForegroundProperty,
                        Brushes.UIText);
                    _underline.Visibility = Visibility.Hidden;
                }
            }
        }

        private void ButtonClicked(object sender, RoutedEventArgs e)
        {
            if (_selected)
            {
                // already selected. Do nothing
                return;
            }
            else
            {
                Selected = true;
            }
        }

        private int _count;
        public int Count
        {
            get
            {
                return _count;
            }
            set
            {
                _count = value;
                if (_count > 0)
                {
                    _textBlockCount.Text = _count.ToString(CultureInfo.CurrentCulture);
                    _textBlockCountContainer.Visibility = Visibility.Visible;
                }
                else
                {
                    _textBlockCountContainer.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void _labelText_MouseEnter(object sender, MouseEventArgs e)
        {
            _labelText.SetResourceReference(
                TextBlock.ForegroundProperty,
                Brushes.ContentSelectedBrushKey);
        }

        private void _labelText_MouseLeave(object sender, MouseEventArgs e)
        {
            if (!_selected)
            {
                _labelText.SetResourceReference(
                    TextBlock.ForegroundProperty,
                    Brushes.UIText);
            }
        }
    }
}