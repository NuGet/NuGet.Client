// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Interaction logic for PackageMetadata.xaml
    /// </summary>
    public partial class PackageMetadataControl : UserControl
    {
        public PackageMetadataControl()
        {
            InitializeComponent();

            Visibility = Visibility.Collapsed;
            DataContextChanged += PackageMetadataControl_DataContextChanged;
        }

        private void PackageMetadataControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (DataContext is DetailedPackageMetadata)
            {
                Visibility = Visibility.Visible;
            }
            else
            {
                Visibility = Visibility.Collapsed;
            }
        }
    }

    /// <summary>
    /// DateTime Converter for published date in package metadata.
    /// </summary>
    public class DateTimeConverter : IValueConverter
    {
        /// <summary>
        /// Converts DateTime object into custom formatting.
        /// Goal of the converter is to have custom formatting for japanese since "D" formatting does not include day of the week.
        /// </summary>
        /// <returns>string value of the date time.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return null;
            }

            var dateTime = DateTime.Parse(value.ToString());
            if (string.Equals(culture.Name, "ja-JP"))
            {
                return $"{dateTime.ToString("D", culture)} {dateTime.ToString("dddd", culture)} ({ dateTime.ToString("d", culture)})";
            }
            else
            {
                return $"{dateTime.ToString("D", culture)} ({ dateTime.ToString("d", culture)})";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
