using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace NuGet.PackageManagement.UI
{
    internal class LoadingStatusToIconConverter : IValueConverter
    {
        private static readonly BitmapImage LoadingIcon;
        private static readonly BitmapImage ReadyIcon;
        private static readonly BitmapImage ErrorIcon;
        private static readonly BitmapImage DefaultIcon;

        static LoadingStatusToIconConverter()
        {
            LoadingIcon = new BitmapImage(new Uri("pack://application:,,,/NuGet.PackageManagement.UI;component/Resources/StatusAnnotations_Play_32xLG_color.png", UriKind.Absolute));
            LoadingIcon?.Freeze();
            ReadyIcon = new BitmapImage(new Uri("pack://application:,,,/NuGet.PackageManagement.UI;component/Resources/StatusOK_32x.png", UriKind.Absolute));
            ReadyIcon?.Freeze();
            ErrorIcon = new BitmapImage(new Uri("pack://application:,,,/NuGet.PackageManagement.UI;component/Resources/StatusStop_32x.png", UriKind.Absolute));
            ErrorIcon?.Freeze();
            DefaultIcon = new BitmapImage(new Uri("pack://application:,,,/NuGet.PackageManagement.UI;component/Resources/packageicon.png", UriKind.Absolute));
            DefaultIcon?.Freeze();
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var loadingStatus = (LoadingStatus)value;
            switch (loadingStatus)
            {
                case LoadingStatus.Loading:
                    return LoadingIcon;
                case LoadingStatus.Cancelled:
                case LoadingStatus.ErrorOccured:
                    return ErrorIcon;
                case LoadingStatus.NoItemsFound:
                case LoadingStatus.NoMoreItems:
                case LoadingStatus.Ready:
                    return ReadyIcon;
                case LoadingStatus.Unknown:
                default:
                    return DefaultIcon;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
