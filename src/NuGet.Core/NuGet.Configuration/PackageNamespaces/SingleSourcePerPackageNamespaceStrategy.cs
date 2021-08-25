// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Globalization;
using NuGet.Common;

namespace NuGet.Configuration
{
    /// <summary>
    /// Strict mode behavior for package namespaces.    
    /// </summary>
    internal class SingleSourcePerPackageNamespaceModeStrategy : INamespaceModeStrategy
    {
        private static readonly ValidationResult SuccessValidationResult = new() { Success = true, ErrorCode = NuGetLogCode.Undefined };

        /// <summary>
        /// Any package id must match have only one matching namespace declaration.
        /// </summary>
        /// <param name="packageId"></param>
        /// <param name="sources">List of filtered package sources</param>
        /// <returns><see cref="ValidationResult"/></returns>
        public ValidationResult ValidateRule(string packageId, IReadOnlyList<string> sources)
        {
            if (sources == null || sources.Count == 0)
            {
                return new()
                {
                    Success = false,
                    ErrorCode = NuGetLogCode.NU1110,
                    ErrorMessage = string.Format(CultureInfo.CurrentCulture,
                    Resources.Error_SingleSourcePerPackageModeNoSources,
                    packageId)
                };
            }
            else if (sources.Count == 1)
            {
                return SuccessValidationResult;
            }

            return new()
            {
                Success = false,
                ErrorCode = NuGetLogCode.NU1110,
                ErrorMessage = string.Format(CultureInfo.CurrentCulture,
                                Resources.Error_SingleSourcePerPackageModeMoreSources,
                                packageId,
                                string.Join(",", sources))
            };
        }
    }
}
