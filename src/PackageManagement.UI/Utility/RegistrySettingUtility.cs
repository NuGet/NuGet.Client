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