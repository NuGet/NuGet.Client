// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Globalization;

namespace NuGet.Packaging
{
    internal static class StringFormatter
    {
        internal static string Log_InstalledPackage(
            string packageId,
            string packageVersion,
            string source,
            string contentHash)
        {
            return string.Format(CultureInfo.CurrentCulture,
                Strings.Log_InstalledPackage,
                packageId,
                packageVersion,
                source,
                contentHash);
        }

        internal static string ZipFileTimeStampModified(
            string entry,
            string originalLastWriteTimeStamp,
            string updatedLastWriteTimeStamp)
        {
            return string.Format(CultureInfo.CurrentCulture,
                Strings.ZipFileTimeStampModified,
                entry,
                originalLastWriteTimeStamp,
                updatedLastWriteTimeStamp);
        }
    }
}
