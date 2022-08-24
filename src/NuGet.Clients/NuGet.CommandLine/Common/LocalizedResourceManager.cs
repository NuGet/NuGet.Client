// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Resources;
using System.Threading;

namespace NuGet.CommandLine
{
    /// <summary>
    /// A wrapper for string resources located at NuGetResources.resx
    /// </summary>
    internal static class LocalizedResourceManager
    {
        private static readonly ResourceManager _resourceManager = new ResourceManager("NuGet.CommandLine.NuGetResources", typeof(LocalizedResourceManager).Assembly);

        public static string GetString(string resourceName)
        {
            return GetString(resourceName, _resourceManager);
        }

        internal static string GetString(string resourceName, ResourceManager resourceManager)
        {
            if (string.IsNullOrEmpty(resourceName))
            {
                throw new ArgumentException("cannot be null or empty", nameof(resourceName));
            }
            if (resourceManager == null)
            {
                throw new ArgumentNullException(nameof(resourceManager));
            }

            CultureInfo cultureNeutral = GetNeutralCulture(Thread.CurrentThread.CurrentUICulture);
            string localizedString = resourceManager.GetString(resourceName, cultureNeutral);
            if (localizedString == null) // can be empty if .resx has an empty string
            {
                // Fallback on existing method
                string culture = GetLanguageName(cultureNeutral);
                return resourceManager.GetString(resourceName + '_' + culture, CultureInfo.InvariantCulture) ??
                       resourceManager.GetString(resourceName, CultureInfo.InvariantCulture);
            }

            return localizedString;
        }

        private static CultureInfo GetNeutralCulture(CultureInfo inputCulture)
        {
            CultureInfo culture = inputCulture;
            while (!culture.IsNeutralCulture)
            {
                if (culture.Parent == culture)
                {
                    break;
                }

                culture = culture.Parent;
            }

            return culture;
        }

        /// <summary>
        /// Returns the 3 letter language name used to locate localized resources.
        /// </summary>
        /// <returns>the 3 letter language name used to locate localized resources.</returns>
        public static string GetLanguageName()
        {
            CultureInfo culture = GetNeutralCulture(Thread.CurrentThread.CurrentUICulture);

            return GetLanguageName(culture);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1308:NormalizeStringsToUppercase", Justification = "the convention is to used lower case letter for language name.")]
        private static string GetLanguageName(CultureInfo culture) => culture.ThreeLetterWindowsLanguageName.ToLowerInvariant();
    }
}
