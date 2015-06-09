using Microsoft.Win32;

namespace NuGet.PackageManagement.UI
{
    internal static class RegistrySettingUtility
    {
        public static void SetBooleanSetting(string key, bool value)
        {
            try
            {
                var nugetRegistrykey = Registry.CurrentUser.CreateSubKey(Constants.NuGetRegistryKey);
                nugetRegistrykey.SetValue(
                    key,
                    value ? "1" : "0",
                    RegistryValueKind.String);
            }
            catch
            {
            }
        }

        /// <summary>
        /// Gets the boolean value of a setting from the registry.
        /// </summary>
        /// <param name="key">the name of the registry key.</param>
        /// <returns>True if the value of the registry key is not "0". Otherwise, false.</returns>
        public static bool GetBooleanSetting(string key)
        {
            try
            {
                var nugetRegistrykey = Registry.CurrentUser.OpenSubKey(Constants.NuGetRegistryKey);
                var keyValue = nugetRegistrykey == null ?
                    null :
                    nugetRegistrykey.GetValue(key) as string;

                return keyValue != null && keyValue != "0";
            }
            catch
            {
                return false;
            }
        }
    }
}