// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Configuration;

namespace NuGet.PackageManagement.VisualStudio.Options
{
    internal static class PackageSourceValidator
    {
        internal static PackageSource FindExistingOrCreate(
            string packageSourceId,
            string source,
            string name,
            bool isEnabled,
            List<PackageSource> packageSources)
        {
            string trimmedSourceId = packageSourceId?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(trimmedSourceId))
            {
                throw new ArgumentException(message: Strings.Argument_Cannot_Be_Null_Or_Empty, paramName: nameof(packageSourceId));
            }

            string trimmedSource = source?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(trimmedSource))
            {
                throw new ArgumentException(message: Strings.Argument_Cannot_Be_Null_Or_Empty, paramName: nameof(source));
            }

            string trimmedName = name?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(trimmedName))
            {
                throw new ArgumentException(message: Strings.Argument_Cannot_Be_Null_Or_Empty, paramName: nameof(name));
            }

            PackageSource? foundById = FindById(trimmedSourceId, packageSources);
            PackageSource packageSource;

            // Create and validate a new Package Source since an existing one was not found.
            if (foundById is null)
            {
                packageSource = new PackageSource(trimmedSource, trimmedName, isEnabled);
                SetAllowInsecureConnectionsProperty(packageSource);
                EnsureValidSources(packageSource);
            }
            else // Found an existing source to update.
            {
                bool isHttpSourceChanged =
                    foundById.IsHttp
                    && !string.Equals(
                        trimmedSource,
                        foundById.Source,
                        StringComparison.OrdinalIgnoreCase);

                // Preserve existing properties by cloning the package source.
                packageSource = new PackageSource(
                    trimmedSource,
                    trimmedName,
                    isEnabled,
                    foundById.IsOfficial,
                    foundById.IsPersistable)
                {
                    IsMachineWide = foundById.IsMachineWide,
                    Credentials = foundById.Credentials,
                    ClientCertificates = foundById.ClientCertificates,
                    Description = foundById.Description,
                    ProtocolVersion = foundById.ProtocolVersion,
                    AllowInsecureConnections = foundById.AllowInsecureConnections,
                    DisableTLSCertificateValidation = foundById.DisableTLSCertificateValidation,
                    MaxHttpRequestsPerSource = foundById.MaxHttpRequestsPerSource,
                };

                if (isHttpSourceChanged)
                {
                    SetAllowInsecureConnectionsProperty(packageSource);
                }
            }

            return packageSource;
        }

        /// <summary>
        /// Validates the Uri of a remote or local package source.
        /// The regex used here will eventually be supported in the Unified Settings registration.json file
        /// for the package sources page. See https://github.com/NuGet/Home/issues/14358.
        /// </summary>
        /// <param name="packageSource"></param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        internal static void EnsureValidSources(PackageSource packageSource)
        {
            _ = packageSource ?? throw new ArgumentNullException(nameof(packageSource));
            string source = packageSource.Source;

            if (!Common.PathValidator.IsValidLocalPath(source) &&
                !Common.PathValidator.IsValidUncPath(source) &&
                !Common.PathValidator.IsValidUrl(source))
            {
                throw new ArgumentOutOfRangeException(
                    paramName: nameof(PackageSource.Source),
                    actualValue: source,
                    Strings.Error_PackageSource_InvalidSource);
            }
        }

        internal static void ValidateUniquenessOrThrow(List<PackageSource> packageSources)
        {
            _ = packageSources ?? throw new ArgumentNullException(nameof(packageSources));

            EnsureUniqueNames(packageSources);
            EnsureUniqueSources(packageSources);
        }

        private static void EnsureUniqueNames(List<PackageSource> packageSources)
        {
            var seen = new HashSet<string>(
                capacity: packageSources.Count,
                comparer: StringComparer.CurrentCultureIgnoreCase);

            foreach (PackageSource packageSource in packageSources)
            {
                if (!seen.Add(packageSource.Name.Trim()))
                {
                    throw new ArgumentException(message: Strings.Error_PackageSource_UniqueName);
                }
            }
        }

        private static void EnsureUniqueSources(List<PackageSource> packageSources)
        {
            var seen = new HashSet<string>(
                capacity: packageSources.Count,
                comparer: StringComparer.OrdinalIgnoreCase);

            foreach (PackageSource packageSource in packageSources)
            {
                string trimmedSource = packageSource.Source?.Trim() ?? string.Empty;

                bool isDuplicate;
                if (packageSource.IsLocal)
                {
                    string canonicalPath = PathValidator.GetCanonicalPath(trimmedSource);
                    isDuplicate = !seen.Add(canonicalPath);
                }
                else
                {
                    isDuplicate = !seen.Add(trimmedSource);
                }

                if (isDuplicate)
                {
                    throw new ArgumentException(message: Strings.Error_PackageSource_UniqueSource);
                }
            }
        }

        private static void SetAllowInsecureConnectionsProperty(PackageSource packageSource)
        {
            _ = packageSource ?? throw new ArgumentNullException(nameof(packageSource));

            if (packageSource.IsHttp && !packageSource.IsHttps)
            {
                packageSource.AllowInsecureConnections = true;
            }

            // An HTTP source has been changed to HTTPS, so allowing insecure connections
            // is no longer needed.
            if (packageSource.AllowInsecureConnections && packageSource.IsHttps)
            {
                packageSource.AllowInsecureConnections = false;
            }
        }

        private static PackageSource? FindById(string packageSourceId, List<PackageSource> packageSources)
        {
            _ = packageSources ?? throw new ArgumentNullException(nameof(packageSources));

            List<PackageSource> existingPackageSource = packageSources
                .Where(packageSource =>
                    string.Equals(packageSource.Name, packageSourceId, StringComparison.CurrentCultureIgnoreCase))
                .ToList();

            if (existingPackageSource.Count > 1)
            {
                throw new InvalidOperationException(message: Strings.Error_PackageSource_UniqueName);
            }

            return existingPackageSource.SingleOrDefault();
        }
    }
}
