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
        private static readonly ValidationResult SuccessValidationResult = new() { Success = true, ErrorCode = NuGetLogCode.Undefined };

        /// <summary>
        /// Any package id must be in one or more matching namespace declarations. 
        /// </summary>
        /// <param name="packageId"></param>
        /// <param name="sources">List of filtered package sources</param> 
        /// <returns>ValidationResult</returns>
        public ValidationResult ValidateRule(string packageId, IReadOnlyList<string> sources)
        {
            if (sources?.Count > 0)
            {
                return SuccessValidationResult;
            }

            return new()
            {
                Success = false,
                ErrorCode = NuGetLogCode.NU1111,
                ErrorMessage = string.Format(CultureInfo.CurrentCulture,
                    Resources.Error_AtLeastOneSourcePerPackageMode,
                    packageId)
            };
        }
    }
}
