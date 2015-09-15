using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NuGet.Configuration;

namespace NuGet
{
    public static class CommandLineUtility
    {
        public readonly static string ApiKeysSectionName = "apikeys";

        public static string GetApiKey(ISettings settings, string source)
        {
            var value = settings.GetDecryptedValue(CommandLineUtility.ApiKeysSectionName, source);
            return value;
        }

        public static void ValidateSource(string source)
        {
            Uri result;
            if (!Uri.TryCreate(source, UriKind.Absolute, out result))
            {
                throw new CommandLineException(LocalizedResourceManager.GetString("InvalidSource"), source);
            }
        }

        public static string GetSourceDisplayName(string source)
        {
            if (String.IsNullOrEmpty(source) || source.Equals(NuGetConstants.DefaultGalleryServerUrl, StringComparison.OrdinalIgnoreCase))
            {
                return LocalizedResourceManager.GetString("LiveFeed") + " (" + NuGetConstants.DefaultGalleryServerUrl + ")";
            }
            if (source.Equals(NuGetConstants.DefaultSymbolServerUrl, StringComparison.OrdinalIgnoreCase))
            {
                return LocalizedResourceManager.GetString("DefaultSymbolServer") + " (" + NuGetConstants.DefaultSymbolServerUrl + ")";
            }
            return "'" + source + "'";
        }

        public static ICollection<PackageReference> GetPackageReferences(PackageReferenceFile configFile, bool requireVersion)
        {
            if (configFile == null)
            {
                throw new ArgumentNullException("configFile");
            }

            var packageReferences = configFile.GetPackageReferences(requireVersion).ToList();
            foreach (var package in packageReferences)
            {
                // GetPackageReferences returns all records without validating values. We'll throw if we encounter packages
                // with malformed ids / Versions.
                if (String.IsNullOrEmpty(package.Id))
                {
                    throw new InvalidDataException(
                        String.Format(CultureInfo.CurrentCulture, LocalizedResourceManager.GetString("InstallCommandInvalidPackageReference"), 
                        configFile.FullPath));
                }
                if (requireVersion && (package.Version == null))
                {
                    throw new InvalidDataException(String.Format(CultureInfo.CurrentCulture, LocalizedResourceManager.GetString("InstallCommandPackageReferenceInvalidVersion"), package.Id));
                }
            }

            return packageReferences;
        }
    }
}