using System.Globalization;

namespace NuGet.Common
{
    public class CultureUtility
    {
        public static void DisableLocalization()
        {
            SetCulture(CultureInfo.InvariantCulture);
        }

        private static void SetCulture(CultureInfo culture)
        {
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
        }
    }
}
