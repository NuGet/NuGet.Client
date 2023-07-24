// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Common
{
    public static class NuGetLogCodeUtility
    {
        public static bool AreVulnerabilitiesInCodes(IEnumerable<NuGetLogCode> nugetLogCodes)
        {
            foreach (var code in nugetLogCodes)
            {
                if (IsVulnerabilityCode(code))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsVulnerabilityCode(NuGetLogCode nuGetLogCode)
        {
            if (nuGetLogCode.Equals(NuGetLogCode.NU1901) ||
                nuGetLogCode.Equals(NuGetLogCode.NU1902) ||
                nuGetLogCode.Equals(NuGetLogCode.NU1903) ||
                nuGetLogCode.Equals(NuGetLogCode.NU1904))
            {
                return true;
            }

            return false;
        }
    }
}
