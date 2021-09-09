// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using Microsoft.VisualStudio.Shell;
using ContractsItemFilter = NuGet.VisualStudio.Internal.Contracts.ItemFilter;

namespace NuGet.PackageManagement.UI
{
    public static class UIUtility
    {
        public static void LaunchExternalLink(Uri url)
        {
            if (url == null
                || !url.IsAbsoluteUri)
            {
                return;
            }

            // mitigate security risk
            if (url.IsFile
                || url.IsLoopback
                || url.IsUnc)
            {
                return;
            }

            if (IsHttpUrl(url))
            {
                // REVIEW: Will this allow a package author to execute arbitrary program on user's machine?
                // We have limited the url to be HTTP only, but is it sufficient?
                // If browser has issues opening the url, unhandled exceptions may crash VS
                try
                {
                    Process.Start(url.AbsoluteUri);
                }
                catch (Exception ex)
                {
                    ActivityLog.LogError(NuGetUI.LogEntrySource, ex.Message);
                }
            }
        }

        private static bool IsHttpUrl(Uri uri)
        {
            return (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        private static readonly string[] ScalingFactor = new string[] {
            string.Empty,
            "K", // kilo
            "M", // mega, million
            "G", // giga, billion
            "T",  // tera, trillion
            "P"  // peta, quadrillion
        };

        // Convert numbers into strings like "1.2K", "33.4M" etc.
        // Precondition: number > 0.
        public static string NumberToString(long number, IFormatProvider culture)
        {
            string format = number >= 950_000 && number <= 999_999 || number >= 950_000_000 && number <= 999_999_999 ? "{0:G4}{1}" : "{0:G3}{1}";

            double v = (double)number;
            int exp = 0;

            while (v >= 1000)
            {
                v = Math.Round(v / 1000, 2);
                ++exp;
            }

            var s = string.Format(
                culture,
                format,
                v,
                ScalingFactor[exp]);
            return s;
        }

        public static string CreateSearchQuery(string query)
        {
            return "packageid:" + query;
        }

        public static ContractsItemFilter ToContractsItemFilter(ItemFilter filter)
        {
            switch (filter)
            {
                case ItemFilter.All:
                    return ContractsItemFilter.All;
                case ItemFilter.Consolidate:
                    return ContractsItemFilter.Consolidate;
                case ItemFilter.Installed:
                    return ContractsItemFilter.Installed;
                case ItemFilter.UpdatesAvailable:
                    return ContractsItemFilter.UpdatesAvailable;
                default:
                    break;
            }

            return ContractsItemFilter.All;
        }
    }
}
