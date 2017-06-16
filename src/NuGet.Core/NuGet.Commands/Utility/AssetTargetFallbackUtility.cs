// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.ProjectModel;

namespace NuGet.Commands
{
    public static class AssetTargetFallbackUtility
    {
        /// <summary>
        /// AssetTargetFallback property name.
        /// </summary>
        public static readonly string AssetTargetFallback = nameof(AssetTargetFallback);

        /// <summary>
        /// Logs an error and returns false if an invalid combination of fallback frameworks
        /// exists in the project.
        /// </summary>
        /// <returns>False if an invalid combination exists.</returns>
        public static async Task<bool> ValidateFallbackFrameworkAsync(PackageSpec spec, ILogger log)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            if (HasInvalidFallbackCombination(spec))
            {
                var error = GetInvalidFallbackCombinationMessage(spec.FilePath);
                await log.LogAsync(error);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Create NU1003 error message.
        /// </summary>
        /// <param name="path">Optional file path.</param>
        public static RestoreLogMessage GetInvalidFallbackCombinationMessage(string path)
        {
            var error = RestoreLogMessage.CreateError(NuGetLogCode.NU1003, Strings.Error_InvalidATF);
            error.ProjectPath = path;
            error.FilePath = path;

            return error;
        }

        /// <summary>
        /// Verify all frameworks have only ATF or PTF.
        /// </summary>
        /// <returns>False if ATF and PTF are both used.</returns>
        public static bool HasInvalidFallbackCombination(PackageSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            return spec.TargetFrameworks.Any(e => e.Imports.Count > 0 && e.AssetTargetFallback.Count > 0);
        }
    }
}
