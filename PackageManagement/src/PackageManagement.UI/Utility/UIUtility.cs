// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
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
    }
}
