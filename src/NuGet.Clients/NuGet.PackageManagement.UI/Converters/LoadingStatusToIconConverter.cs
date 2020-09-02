// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using NuGet.PackageManagement.VisualStudio;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.UI
{
    [ValueConversion(typeof(LoadingStatus), typeof(BitmapImage))]
    internal class LoadingStatusToIconConverter : IValueConverter
    {
        private static readonly BitmapImage ReadyIcon;
        private static readonly BitmapImage ErrorIcon;

        static LoadingStatusToIconConverter()
        {
            ReadyIcon = new BitmapImage(new Uri("pack://application:,,,/NuGet.PackageManagement.UI;component/Resources/StatusOK_32x.png", UriKind.Absolute));
            ReadyIcon?.Freeze();
            ErrorIcon = new BitmapImage(new Uri("pack://application:,,,/NuGet.PackageManagement.UI;component/Resources/StatusStop_32x.png", UriKind.Absolute));
            ErrorIcon?.Freeze();
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var loadingStatus = (LoadingStatus)value;
            switch (loadingStatus)
            {
                case LoadingStatus.Cancelled:
                case LoadingStatus.ErrorOccurred:
                    return ErrorIcon;
                case LoadingStatus.Loading:
                case LoadingStatus.NoItemsFound:
                case LoadingStatus.NoMoreItems:
                case LoadingStatus.Ready:
                    return ReadyIcon;
                case LoadingStatus.Unknown:
                default:
                    return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
