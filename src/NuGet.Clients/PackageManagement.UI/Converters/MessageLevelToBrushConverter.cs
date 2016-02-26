using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace NuGet.PackageManagement.UI
{
    internal class MessageLevelToBrushConverter : IValueConverter
    {
        private static readonly object GreenBrush = (SolidColorBrush)(new BrushConverter().ConvertFrom("#54d360"));
        private static readonly object YellowBrush = (SolidColorBrush)(new BrushConverter().ConvertFrom("#fef5b7"));
        private static readonly object RedBrush = (SolidColorBrush)(new BrushConverter().ConvertFrom("#febeb7"));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var messageLevel = (MessageLevel)value;
            switch(messageLevel)
            {
                case MessageLevel.Error:
                    return RedBrush;
                case MessageLevel.Info:
                    return GreenBrush;
                case MessageLevel.Warning:
                    return YellowBrush;
                default:
                    return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
