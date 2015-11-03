﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Resx = NuGet.PackageManagement.UI.Resources;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Interaction logic for AuthorAndDownloadCount.xaml. This control is used to display
    /// the author and download count information of a package.
    /// </summary>
    public partial class AuthorAndDownloadCount : UserControl, INotifyPropertyChanged
    {
        public static readonly DependencyProperty AuthorProperty =
            DependencyProperty.Register(
                nameof(Author),
                typeof(string),
                typeof(AuthorAndDownloadCount),
                new PropertyMetadata(OnPropertyChanged));

        public static readonly DependencyProperty DownloadCountProperty =
            DependencyProperty.Register(
                nameof(DownloadCount),
                typeof(long?),
                typeof(AuthorAndDownloadCount),
                new PropertyMetadata(OnPropertyChanged));

        public AuthorAndDownloadCount()
        {
            InitializeComponent();
        }

        public long? DownloadCount
        {
            get
            {
                return GetValue(DownloadCountProperty) as long?;
            }
            set
            {
                SetValue(DownloadCountProperty, value);
                UpdateControl();
            }
        }

        private static void OnPropertyChanged(
            DependencyObject dependencyObject, 
            DependencyPropertyChangedEventArgs e)
        {
            var control = dependencyObject as AuthorAndDownloadCount;
            control?.UpdateControl();
        }

        public string Author
        {
            get
            {
                return GetValue(AuthorProperty) as string;
            }
            set
            {
                SetValue(AuthorProperty, value);
                UpdateControl();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private void UpdateControl()
        {
            if (!string.IsNullOrEmpty(Author))
            {
                _textBlockAuthor.Text = string.Format(
                    CultureInfo.CurrentCulture,
                    Resx.Text_ByAuthor,
                    Author);
                _textBlockAuthor.Visibility = Visibility.Visible;
            }
            else
            {
                _textBlockAuthor.Visibility = Visibility.Collapsed;
            }

            // Generate the textbox for download count.
            _textBlockDownloadCount.Inlines.Clear();

            if (DownloadCount.HasValue && DownloadCount.Value > 0)
            {   
                // Processing the format string ourselves. We only support "{0}".
                var formatString = Resx.Text_Downloads;
                string begin = string.Empty;
                string end = string.Empty;
                var index = formatString.IndexOf("{0}");
                if (index == -1)
                {
                    // Cannot find "{0}".
                    Debug.Fail("Label_ConsiderUsing does not contain {0}");
                    begin = formatString;
                }
                else
                {
                    begin = formatString.Substring(0, index);
                    end = formatString.Substring(index + "{0}".Length);
                }

                _textBlockDownloadCount.Inlines.Add(new Run(begin)); 
                _textBlockDownloadCount.Inlines.Add(
                    new Run(UIUtility.NumberToString(DownloadCount.Value))
                    {
                        FontWeight = FontWeights.Bold
                    });
                _textBlockDownloadCount.Inlines.Add(new Run(end));
                _textBlockDownloadCount.Visibility = Visibility.Visible;
            }        
            else
            {
                _textBlockDownloadCount.Visibility = Visibility.Collapsed;
            }

            // set the visiblity of the separator.
            if (_textBlockAuthor.Visibility == Visibility.Visible &&
                _textBlockDownloadCount.Visibility == Visibility.Visible)
            {
                _separator.Visibility = Visibility.Visible;
            }
            else
            {
                _separator.Visibility = Visibility.Collapsed;
            }

            // set the visibility of the control itself.
            if (_textBlockAuthor.Visibility == Visibility.Collapsed &&
                _textBlockDownloadCount.Visibility == Visibility.Collapsed)
            {
                _self.Visibility = Visibility.Collapsed;
            }
            else
            {
                _self.Visibility = Visibility.Visible;
            }
        }
    }
}