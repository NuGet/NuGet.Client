using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace NuGet.PackageManagement.UI
{
    [ValueConversion(typeof(MessageLevel), typeof(Brush))]
    internal class MessageLevelToBrushConverter : Freezable, IValueConverter
    {
        public static readonly DependencyProperty WarningProperty =
            DependencyProperty.Register(nameof(Warning), typeof(Brush), typeof(MessageLevelToBrushConverter));

        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register(nameof(Message), typeof(Brush), typeof(MessageLevelToBrushConverter));

        public Brush Warning
        {
            get { return (Brush)GetValue(WarningProperty); }
            set { SetValue(WarningProperty, value); }
        }

        public Brush Message
        {
            get { return (Brush)GetValue(MessageProperty); }
            set { SetValue(MessageProperty, value); }
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var messageLevel = (MessageLevel)value;
            switch (messageLevel)
            {
                case MessageLevel.Error:
                case MessageLevel.Warning:
                    return Warning;

                case MessageLevel.Info:
                default:
                    return Message;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var messageLevel = (MessageLevel)values[0];
            switch (messageLevel)
            {
                case MessageLevel.Error:
                case MessageLevel.Warning:
                    return Warning;

                case MessageLevel.Info:
                default:
                    return Message;
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        protected override Freezable CreateInstanceCore() => new MessageLevelToBrushConverter();
    }
}