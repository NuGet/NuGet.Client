// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Globalization;

namespace NuGet.Commands
{
    public static class StringFormatter
    {
        public static string Log_TrustedSignerAlreadyExists(
            string packageId)
        {
            return string.Format(CultureInfo.CurrentCulture,
                Strings.Error_TrustedSignerAlreadyExists,
                packageId);
        }

        public static string Log_TrustedRepoAlreadyExists(
            string serviceIndex)
        {
            return string.Format(CultureInfo.CurrentCulture,
                Strings.Error_TrustedRepoAlreadyExists,
                serviceIndex);
        }

        public static string Log_TrustFingerPrintAlreadyExist()
        {
            return string.Format(CultureInfo.CurrentCulture,
                Strings.Error_TrustFingerPrintAlreadyExist);
        }
    }
}
