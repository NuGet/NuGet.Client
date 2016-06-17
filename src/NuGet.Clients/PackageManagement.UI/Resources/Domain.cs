using System.ComponentModel;
using System.Windows;

namespace NuGet.PackageManagement.UI
{
    public sealed class Domain
    {
        private static readonly bool _isInDesignMode = GetDesignModePropertyValue();

        /// <summary>
        /// Gets a value indicating whether the control is in design mode (running in Blend
        /// or Visual Studio).
        /// </summary>
        public static bool IsInDesignMode => _isInDesignMode;

        private static bool GetDesignModePropertyValue()
        {
            var prop = DesignerProperties.IsInDesignModeProperty;
            return (bool)DependencyPropertyDescriptor
                .FromProperty(prop, typeof(FrameworkElement))
                .Metadata
                .DefaultValue;
        }

        public static bool IsInStandaloneMode => StandaloneSwitch.IsRunningStandalone;

        public static Visibility HiddenWhenNotInDesignMode => IsInDesignMode ? Visibility.Visible : Visibility.Hidden;

        public static Visibility CollapsedWhenNotInDesignMode => IsInDesignMode ? Visibility.Visible : Visibility.Collapsed;
    }
}
