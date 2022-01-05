// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Shared;

namespace NuGet.Commands.PackCommand
{
    /// <summary>
    /// Contains Package specific properties for Warnings.
    /// </summary>
    public class PackageSpecificWarningProperties
    {
        /// <summary>
        /// Contains Package specific No warn properties.
        /// NuGetLogCode -> LibraryId -> Set of Frameworks.
        /// </summary>
        public IDictionary<NuGetLogCode, IDictionary<string, ISet<NuGetFramework>>> Properties { get; private set; }

        /// <summary>
        /// Extracts PackageSpecific WarningProperties from a PackageSpec
        /// </summary>
        /// <param name="noWarnProperties">noWarnProperties containing the Dependencies with WarningProperties</param>
        /// <returns>PackageSpecific WarningProperties extracted from a noWarnProperties</returns>
        public static PackageSpecificWarningProperties CreatePackageSpecificWarningProperties(IDictionary<string, HashSet<(NuGetLogCode, NuGetFramework)>> noWarnProperties)
        {
            if (noWarnProperties == null)
                return null;

            // NuGetLogCode -> LibraryId -> Set of Frameworks.
            var warningProperties = new PackageSpecificWarningProperties();

            foreach (KeyValuePair<string, HashSet<(NuGetLogCode, NuGetFramework)>> packageNoWarnProperty in noWarnProperties)
            {
                string packageId = packageNoWarnProperty.Key;

                foreach ((NuGetLogCode nuGetLogCode, NuGetFramework nuGetFramework) propertyPair in packageNoWarnProperty.Value)
                {
                    warningProperties.Add(propertyPair.nuGetLogCode, packageId, propertyPair.nuGetFramework);
                }
            }

            return warningProperties;
        }

        /// <summary>
        /// Adds a NuGetLogCode into the NoWarn Set for the specified library Id and target graph.
        /// </summary>
        /// <param name="code">NuGetLogCode for which no warning should be thrown.</param>
        /// <param name="libraryId">Library for which no warning should be thrown.</param>
        /// <param name="framework">Target graph for which no warning should be thrown.</param>
        internal void Add(NuGetLogCode code, string libraryId, NuGetFramework framework)
        {
            if (Properties == null)
            {
                Properties = new Dictionary<NuGetLogCode, IDictionary<string, ISet<NuGetFramework>>>();
            }

            if (!Properties.ContainsKey(code))
            {
                Properties.Add(code, new Dictionary<string, ISet<NuGetFramework>>(StringComparer.OrdinalIgnoreCase));
            }

            if (Properties[code].ContainsKey(libraryId))
            {
                Properties[code][libraryId].Add(framework);
            }
            else
            {
                Properties[code].Add(libraryId, new HashSet<NuGetFramework>(new NuGetFrameworkFullComparer()) { framework });
            }
        }

        /// <summary>
        /// Checks if a NugetLogCode is part of the NoWarn list for the specified library Id and target graph.
        /// </summary>
        /// <param name="code">NugetLogCode to be checked.</param>
        /// <param name="libraryId">library Id to be checked.</param>
        /// <param name="framework">target graph to be checked.</param>
        /// <returns>True if the NugetLogCode is part of the NoWarn list for the specified libraryId and Target Graph.</returns>
        public bool Contains(NuGetLogCode code, string libraryId, NuGetFramework framework)
        {
            return Properties != null &&
                Properties.TryGetValue(code, out var libraryIdsAndFrameworks) &&
                libraryIdsAndFrameworks.TryGetValue(libraryId, out var frameworkSet) &&
                frameworkSet.Contains(framework);
        }
    }
}
