// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using NuGet.Shared;

namespace NuGet.Commands
{

    /// <summary>
    /// Contains Package specific properties for Warnings.
    /// </summary>
    public class PackageSpecificWarningProperties : IEquatable <PackageSpecificWarningProperties>
    {

        /// <summary>
        /// Contains Package specific No warn properties.
        /// NuGetLogCode -> LibraryId -> Set of Frameworks.
        /// </summary>
        public IDictionary<NuGetLogCode, IDictionary<string, ISet<NuGetFramework>>> Properties { get; private set; }

        /// <summary>
        /// Extracts PackageSpecific WarningProperties from a PackageSpec
        /// </summary>
        /// <param name="packageSpec">PackageSpec containing the Dependencies with WarningProperties</param>
        /// <returns>PackageSpecific WarningProperties extracted from a PackageSpec</returns>
        public static PackageSpecificWarningProperties CreatePackageSpecificWarningProperties(PackageSpec packageSpec)
        {
            // NuGetLogCode -> LibraryId -> Set of Frameworks.
            var warningProperties = new PackageSpecificWarningProperties();

            foreach (var dependency in packageSpec.Dependencies)
            {
                foreach (var framework in packageSpec.TargetFrameworks)
                {
                    warningProperties.AddRange(dependency.NoWarn, dependency.Name, framework.FrameworkName);
                }
            }

            foreach (var framework in packageSpec.TargetFrameworks)
            {
                foreach (var dependency in framework.Dependencies)
                {
                    warningProperties.AddRange(dependency.NoWarn, dependency.Name, framework.FrameworkName);
                }
            }

            return warningProperties;
        }

        /// <summary>
        /// Extracts PackageSpecific WarningProperties from a PackageSpec for a specific NuGetFramework
        /// </summary>
        /// <param name="packageSpec">PackageSpec containing the Dependencies with WarningProperties</param>
        /// <param name="framework">NuGetFramework for which the properties should be assessed.</param>
        /// <returns>PackageSpecific WarningProperties extracted from a PackageSpec for a specific NuGetFramework</returns>
        public static PackageSpecificWarningProperties CreatePackageSpecificWarningProperties(PackageSpec packageSpec, 
            NuGetFramework framework)
        {
            // NuGetLogCode -> LibraryId -> Set of Frameworks.
            var warningProperties = new PackageSpecificWarningProperties();

            foreach (var dependency in packageSpec.Dependencies)
            {
                warningProperties.AddRange(dependency.NoWarn, dependency.Name, framework);
            }

            var targetFrameworkInformation = packageSpec
                .TargetFrameworks
                .Where(tfi => tfi.FrameworkName == framework)
                .First();
            
            foreach (var dependency in targetFrameworkInformation.Dependencies)
            {
                warningProperties.AddRange(dependency.NoWarn, dependency.Name, framework);
            }

            return warningProperties;
        }

        /// <summary>
        /// Adds a NuGetLogCode into the NoWarn Set for the specified library Id and target graph.
        /// </summary>
        /// <param name="code">NuGetLogCode for which no warning should be thrown.</param>
        /// <param name="libraryId">Library for which no warning should be thrown.</param>
        /// <param name="framework">Target graph for which no warning should be thrown.</param>
        public void Add(NuGetLogCode code, string libraryId, NuGetFramework framework)
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
        /// Adds a list of NuGetLogCode into the NoWarn Set for the specified library Id and target graph.
        /// </summary>
        /// <param name="codes">IEnumerable of NuGetLogCode for which no warning should be thrown.</param>
        /// <param name="libraryId">Library for which no warning should be thrown.</param>
        /// <param name="framework">Target graph for which no warning should be thrown.</param>
        public void AddRange(IEnumerable<NuGetLogCode> codes, string libraryId, NuGetFramework framework)
        {
            foreach (var code in codes)
            {
                Add(code, libraryId, framework);
            }
        }

        /// <summary>
        /// Adds a list of NuGetLogCode into the NoWarn Set for the specified library Id and target graph.
        /// </summary>
        /// <param name="code">NuGetLogCode for which no warning should be thrown.</param>
        /// <param name="libraryId">Library for which no warning should be thrown.</param>
        /// <param name="frameworks">IEnumerable of Target graph for which no warning should be thrown.</param>
        public void AddRange(NuGetLogCode code, string libraryId, IEnumerable<NuGetFramework> frameworks)
        {
            foreach (var framework in frameworks)
            {
                Add(code, libraryId, framework);
            }
        }

        /// <summary>
        /// Checks if a NugetLogCode is part of the NoWarn list for the specified library Id and target graph.
        /// </summary>
        /// <param name="code">NugetLogCode to be checked.</param>
        /// <param name="libraryId">library Id to be checked.</param>
        /// <param name="framework">target graph to be checked.</param>
        /// <returns>True iff the NugetLogCode is part of the NoWarn list for the specified libraryId and Target Graph.</returns>
        public bool Contains(NuGetLogCode code, string libraryId, NuGetFramework framework)
        {
            return Properties != null &&
                Properties.TryGetValue(code, out var libraryIdsAndFrameworks) &&
                libraryIdsAndFrameworks.TryGetValue(libraryId, out var frameworkSet) &&
                frameworkSet.Contains(framework);
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCodeCombiner();

            // Add a constant hash for all objects
            hashCode.AddObject(1);

            return hashCode.CombinedHash;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PackageSpecificWarningProperties);
        }

        public bool Equals(PackageSpecificWarningProperties other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Properties.Keys.OrderedEquals(other.Properties.Keys, (code) => code) &&
                Properties.Keys.All(c => Properties[c].Keys.OrderedEquals(other.Properties[c].Keys, (id) => id)) &&
                Properties.Keys.All(c => Properties[c].Keys.All(id => Properties[c][id].SetEquals(other.Properties[c][id])));
        }
    }
}
