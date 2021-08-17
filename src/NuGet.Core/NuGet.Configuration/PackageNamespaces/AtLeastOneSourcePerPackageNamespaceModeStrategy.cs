// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Globalization;
using NuGet.Common;

namespace NuGet.Configuration
{
    /// <summary>
    /// Relaxed mode behavior for package namespaces.    
    /// </summary>
    internal class AtLeastOneSourcePerPackageNamespaceModeStrategy : INamespaceModeStrategy
    {
        /// <summary>
        /// Any package id must be in one or more matching namespace declarations. 
        /// </summary>
        /// <param name="packageId"></param>
        /// <param name="sources">List of filtered package sources</param>
        /// <param name="errormessage">Non empty string incase business rule validation fails</param>
        /// <returns>true if number of sources satisy business rule otherwise false</returns>
        public bool TryValidate(string packageId, IReadOnlyList<string> sources, out string errormessage)
        {
            errormessage = string.Empty;

            if (sources?.Count > 0)
                return true;

            errormessage = string.Format(CultureInfo.CurrentCulture,
                    Resources.Error_AtLeastOneSourcePerPackageMode,
                    NuGetLogCode.NU1111,
                    packageId);

            return false;
        }
    }
}
