namespace NuGet.PackageManagement.UI
{
    internal static class Constants
    {
        public const string NuGetRegistryKey = @"Software\NuGet";
        public const string SuppressUIDisclaimerRegistryName = "SuppressUILegalDisclaimer";
        public const string DoNotShowPreviewWindowRegistryName = "DoNotShowPreviewWindow";
        public const string IncludePrereleaseRegistryName = "IncludePrerelease";

        /// <summary>
        /// This is the registry key which tracks whether to show the deprecated framework window.
        /// </summary>
        public static readonly string DoNotShowDeprecatedFrameworkWindowRegistryName = "DoNotShowDeprecatedFrameworkWindow";
    }
}