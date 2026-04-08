// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Globalization;
using NuGet.Common;
using NuGet.Packaging;

namespace NuGet.Protocol
{
    internal static class PackageIdValidator
    {
        private const string DisableValidationEnvVar = "NUGET_DISABLE_PACKAGEID_VALIDATION";

        private static readonly Lazy<bool> _isValidationDisabled = new Lazy<bool>(() =>
            string.Equals(
                EnvironmentVariableWrapper.Instance.GetEnvironmentVariable(DisableValidationEnvVar),
                "true",
                StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Validates the package ID content.
        /// </summary>
        /// <param name="packageId">The package ID to validate.</param>
        /// <exception cref="InvalidPackageIdException">
        /// Thrown if <paramref name="packageId"/> is not a valid NuGet package ID.
        /// </exception>
        internal static void Validate(string packageId, IEnvironmentVariableReader env = null)
        {
            bool isDisabled = env == null
                ? _isValidationDisabled.Value
                : string.Equals(env.GetEnvironmentVariable(DisableValidationEnvVar), "true", StringComparison.OrdinalIgnoreCase);

            if (!isDisabled)
            {
                if (!Packaging.PackageIdValidator.IsValidPackageId(packageId))
                {
                    throw new InvalidPackageIdException(string.Format(CultureInfo.CurrentCulture, Strings.Error_Invalid_package_id, packageId));
                }
            }
        }
    }
}
