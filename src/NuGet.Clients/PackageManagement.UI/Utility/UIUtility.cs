// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Globalization;
using Microsoft.VisualStudio.Shell;

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

                NuGetEventTrigger.Instance.TriggerEvent(NuGetEvent.LinkOpened);
            }
        }

        private static bool IsHttpUrl(Uri uri)
        {
            return (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        private static readonly string[] _scalingFactor = new string[] {
            String.Empty,
            "K", // kilo
            "M", // mega, million
            "G", // giga, billion
            "T"  // tera, trillion
        };

        // Convert numbers into strings like "1.2K", "33.4M" etc.
        // Precondition: number > 0.
        public static string NumberToString(long number)
        {
            double v = (double)number;
            int exp = 0;

            while (v >= 1000)
            {
                v /= 1000;
                ++exp;
            }

            var s = string.Format(
                CultureInfo.CurrentCulture,
                "{0:G3}{1}",
                v,
                _scalingFactor[exp]);
            return s;
        }
    }
}