// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Shared;

namespace NuGet.ProjectModel
{
    public class TargetFrameworkInformation : IEquatable<TargetFrameworkInformation>
    {
        private IEnumerable<CentralVersionDependency> _centralVersionDependecies = new List<CentralVersionDependency>();

        public NuGetFramework FrameworkName { get; set; }

        public IList<LibraryDependency> Dependencies { get; set; } = new List<LibraryDependency>();

        /// <summary>
        /// A fallback PCL framework to use when no compatible items
        /// were found for <see cref="FrameworkName"/>.
        /// </summary>
        public IList<NuGetFramework> Imports { get; set; } = new List<NuGetFramework>();

        /// <summary>
        /// If True AssetTargetFallback behavior will be used for Imports.
        /// </summary>
        public bool AssetTargetFallback { get; set; }

        /// <summary>
        /// Display warnings when the Imports framework is used.
        /// </summary>
        public bool Warn { get; set; }

        /// <summary>
        /// List of dependencies that are not part of the graph resolution.
        /// </summary>
        public IList<DownloadDependency> DownloadDependencies { get; } = new List<DownloadDependency>();

        /// <summary>
        /// List of the package versions defined in the Central package versions management file. 
        /// </summary>
        public IReadOnlyList<CentralVersionDependency> CentralVersionDependencies { get { return _centralVersionDependecies.ToList(); } }

        /// <summary>
        /// A set of unique FrameworkReferences
        /// </summary>
        public ISet<FrameworkDependency> FrameworkReferences { get; } = new HashSet<FrameworkDependency>();

        /// <summary>
        /// The project provided runtime.json
        /// </summary>
        public string RuntimeIdentifierGraphPath { get; set; }

        public override string ToString()
        {
            return FrameworkName.GetShortFolderName();
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCodeCombiner();

            hashCode.AddObject(FrameworkName);
            hashCode.AddObject(AssetTargetFallback);
            hashCode.AddSequence(Dependencies);
            hashCode.AddSequence(Imports);
            hashCode.AddSequence(DownloadDependencies);
            hashCode.AddSequence(FrameworkReferences);
            hashCode.AddObject(RuntimeIdentifierGraphPath);
            hashCode.AddSequence(CentralVersionDependencies);
            return hashCode.CombinedHash;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as TargetFrameworkInformation);
        }

        public bool Equals(TargetFrameworkInformation other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return EqualityUtility.EqualsWithNullCheck(FrameworkName, other.FrameworkName) &&
                   Dependencies.OrderedEquals(other.Dependencies, dependency => dependency.Name, StringComparer.OrdinalIgnoreCase) &&
                   Imports.SequenceEqualWithNullCheck(other.Imports) &&
                   AssetTargetFallback == other.AssetTargetFallback &&
                   DownloadDependencies.OrderedEquals(other.DownloadDependencies, dep => dep) &&
                   FrameworkReferences.OrderedEquals(other.FrameworkReferences, fr => fr) &&
                   CentralVersionDependencies.OrderedEquals(other.CentralVersionDependencies, centralVersion => centralVersion) &&
                   string.Equals(RuntimeIdentifierGraphPath, other.RuntimeIdentifierGraphPath);
        }

        public TargetFrameworkInformation Clone()
        {
            var clonedObject = new TargetFrameworkInformation();
            clonedObject.FrameworkName = FrameworkName;
            clonedObject.Dependencies = Dependencies.Select(item => item.Clone()).ToList();
            clonedObject.Imports = new List<NuGetFramework>(Imports);
            clonedObject.AssetTargetFallback = AssetTargetFallback;
            clonedObject.Warn = Warn;
            clonedObject.DownloadDependencies.AddRange(DownloadDependencies.Select(item => item.Clone()));
            clonedObject.FrameworkReferences.AddRange(FrameworkReferences);
            clonedObject.RuntimeIdentifierGraphPath = RuntimeIdentifierGraphPath;
            clonedObject._centralVersionDependecies = CentralVersionDependencies.Select(item => item.Clone());
            return clonedObject;
        }

        /// <summary>
        /// It merges the Central Version information to the PackageVersion information.
        /// It removes the duplication between the CentralVersions and the PackageReferences.
        /// </summary>
        internal bool TryArrangeCentralPackageVersions(string projectName, out string error)
        {
            var indexedCPVMInfo = CentralVersionDependencies.ToDictionary(x => x.Name, StringComparer.InvariantCultureIgnoreCase);
            error = null;

            foreach (var d in Dependencies.Where(d => !d.AutoReferenced && !d.LibraryRange.VersionRange.IsCentral))
            {
                // The PackagereReference item should not have an explicit version defined. 
                if(!d.LibraryRange.VersionRange.Default)
                {
                    error = string.Format(CultureInfo.CurrentCulture, Strings.Error_CentralPackageVersions_VersionsNotAllowed, projectName, d.Name);
                    return false;
                }
                if (indexedCPVMInfo.ContainsKey(d.Name))
                {
                    d.LibraryRange = indexedCPVMInfo[d.Name];
                    d.LibraryRange.VersionRange.IsCentral = true;
                    indexedCPVMInfo.Remove(d.Name);
                }              
            }

            _centralVersionDependecies = indexedCPVMInfo.Values.ToList();
            return true;
        }

        public void AddCentralPackageVersionInformation(IEnumerable<CentralVersionDependency> centralVersionDependencies)
        {
            _centralVersionDependecies = centralVersionDependencies;
        }
    }
}
