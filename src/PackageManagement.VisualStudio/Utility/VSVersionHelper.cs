using EnvDTE;
using System;

namespace NuGet.PackageManagement.VisualStudio
{
    public class VSVersionHelper
    {
        private const int MaxVsVersion = 12;
        private static readonly Lazy<int> _vsMajorVersion = new Lazy<int>(GetMajorVsVersion);
        private static readonly Lazy<string> _fullVsEdition = new Lazy<string>(GetFullVsVersionString);

        public static int VsMajorVersion
        {
            get { return _vsMajorVersion.Value; }
        }

        public static bool IsVisualStudio2010
        {
            get { return VsMajorVersion == 10; }
        }

        public static bool IsVisualStudio2012
        {
            get { return VsMajorVersion == 11; }
        }

        public static bool IsVisualStudio2013
        {
            get { return VsMajorVersion == 12; }
        }

        public static bool IsVisualStudio2014
        {
            get { return VsMajorVersion == 14; }
        }

        public static string FullVsEdition
        {
            get { return _fullVsEdition.Value; }
        }

        private static int GetMajorVsVersion()
        {
            DTE dte = ServiceLocator.GetInstance<DTE>();
            string vsVersion = dte.Version;
            Version version;
            if (Version.TryParse(vsVersion, out version))
            {
                return version.Major;
            }
            return MaxVsVersion;
        }

        private static string GetFullVsVersionString()
        {
            DTE dte = ServiceLocator.GetInstance<DTE>();

            string edition = dte.Edition;
            if (!edition.StartsWith("VS", StringComparison.OrdinalIgnoreCase))
            {
                edition = "VS " + edition;
            }

            return edition + "/" + dte.Version;
        }

        public static string GetSKU()
        {
            DTE dte = ServiceLocator.GetInstance<DTE>();
            string sku = dte.Edition;
            if (sku.Equals("Ultimate", StringComparison.OrdinalIgnoreCase) ||
                sku.Equals("Premium", StringComparison.OrdinalIgnoreCase) ||
                sku.Equals("Professional", StringComparison.OrdinalIgnoreCase))
            {
                sku = "Pro";
            }

            return sku;
        }
    }
}
