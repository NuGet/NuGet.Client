using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NuGet.Packaging.Core;
using NuGet.Packaging.PackageCreation.Resources;
using NuGet.Versioning;

namespace NuGet.Packaging
{
    public class ManifestMetadata : IPackageMetadata
    {
        private string _minClientVersionString;

        private IEnumerable<string> _authors = Enumerable.Empty<string>();
        private IEnumerable<string> _owners = Enumerable.Empty<string>();

        private string _iconUrl;
        private string _licenseUrl;
        private string _projectUrl;

        public ManifestMetadata()
        {
        }

        /// <summary>
        /// Constructs a ManifestMetadata instance from an IPackageMetadata instance
        /// </summary>
        public ManifestMetadata(IPackageMetadata copy)
        {
            Id = copy.Id?.Trim();
            Version = copy.Version;
            Title = copy.Title?.Trim();
            Authors = copy.Authors;
            Owners = copy.Owners;
            Tags = string.IsNullOrEmpty(copy.Tags) ? null : copy.Tags.Trim();
            _licenseUrl = copy.LicenseUrl?.OriginalString;
            _projectUrl = copy.ProjectUrl?.OriginalString;
            _iconUrl = copy.IconUrl?.OriginalString;
            RequireLicenseAcceptance = copy.RequireLicenseAcceptance;
            Description = copy.Description?.Trim();
            Copyright = copy.Copyright?.Trim();
            Summary = copy.Summary?.Trim();
            ReleaseNotes = copy.ReleaseNotes?.Trim();
            Language = copy.Language?.Trim();
            DependencyGroups = CreatePackageDependencyGroups(copy.DependencyGroups);
            FrameworkAssemblies = copy.FrameworkAssemblies;
            PackageAssemblyReferences = copy.PackageAssemblyReferences;
            MinClientVersionString = copy.MinClientVersion?.ToString();
            ContentFiles = copy.ContentFiles;
            DevelopmentDependency = copy.DevelopmentDependency;
        }

        [ManifestVersion(5)]
        public string MinClientVersionString
        {
            get { return _minClientVersionString; }
            set
            {
                Version version = null;
                if (!String.IsNullOrEmpty(value) && !System.Version.TryParse(value, out version))
                {
                    throw new InvalidDataException(NuGetResources.Manifest_InvalidMinClientVersion);
                }

                _minClientVersionString = value;
                MinClientVersion = version;
            }
        }

        public Version MinClientVersion { get; private set; }

        public string Id { get; set; }

        public NuGetVersion Version { get; set; }

        public string Title { get; set; }

        public IEnumerable<string> Authors
        {
            get { return _authors; }
            set { _authors = value ?? Enumerable.Empty<string>(); }
        }

        public IEnumerable<string> Owners
        {
            get { return (_owners == null || !_owners.Any()) ? _authors : _owners; }
            set { _owners = value ?? Enumerable.Empty<string>(); }
        }

        // The (Icon/License/Project)Url properties have backing strings as we need to be able to differentiate
        //   between the property not being set (valid) and set to an empty value (invalid). 
        public void SetIconUrl(string iconUrl)
        {
            _iconUrl = iconUrl;
        }

        public Uri IconUrl
        {
            get
            {
                if (_iconUrl == null)
                {
                    return null;
                }

                return new Uri(_iconUrl);
            }
        }

        public void SetLicenseUrl(string licenseUrl)
        {
            _licenseUrl = licenseUrl;
        }

        public Uri LicenseUrl
        {
            get
            {
                if (_licenseUrl == null)
                {
                    return null;
                }

                return new Uri(_licenseUrl);
            }
        }

        public void SetProjectUrl(string projectUrl)
        {
            _projectUrl = projectUrl;
        }

        public Uri ProjectUrl
        {
            get
            {
                if (_projectUrl == null)
                {
                    return null;
                }

                return new Uri(_projectUrl);
            }
        }

        public bool RequireLicenseAcceptance { get; set; }

        public bool DevelopmentDependency { get; set; }

        public string Description { get; set; }

        public string Summary { get; set; }

        [ManifestVersion(2)]
        public string ReleaseNotes { get; set; }

        [ManifestVersion(2)]
        public string Copyright { get; set; }

        public string Language { get; set; }

        public string Tags { get; set; }

        public IEnumerable<PackageDependencyGroup> DependencyGroups { get; set; } = new List<PackageDependencyGroup>();

        public IEnumerable<FrameworkAssemblyReference> FrameworkAssemblies { get; set; } = new List<FrameworkAssemblyReference>();

        [ManifestVersion(2)]
        public ICollection<PackageReferenceSet> PackageAssemblyReferences { get; set; } = new List<PackageReferenceSet>();

        public ICollection<ManifestContentFiles> ContentFiles { get; set; } = new List<ManifestContentFiles>();

        private static IEnumerable<PackageDependencyGroup> CreatePackageDependencyGroups(IEnumerable<PackageDependencyGroup> packageDependencyGroups)
        {
            if (packageDependencyGroups == null || !packageDependencyGroups.Any())
            {
                return new List<PackageDependencyGroup>(0);
            }

            return packageDependencyGroups.Select(dependencyGroup =>
            {
                var dependencies = CreatePackageDependencies(dependencyGroup.Packages);
                return new PackageDependencyGroup(dependencyGroup.TargetFramework, dependencies);
            }).ToList();
        }

        private static IEnumerable<PackageDependency> CreatePackageDependencies(IEnumerable<PackageDependency> packageDependencies)
        {
            if (packageDependencies == null)
            {
                return new List<PackageDependency>(0);
            }

            return packageDependencies.Select(dependency =>
            {
                return new PackageDependency(
                    dependency.Id.SafeTrim(),
                    dependency.VersionRange,
                    dependency.Include,
                    dependency.Exclude);
            }).ToList();
        }

        public IEnumerable<string> Validate()
        {
            if (String.IsNullOrEmpty(Id))
            {
                yield return String.Format(CultureInfo.CurrentCulture, NuGetResources.Manifest_RequiredMetadataMissing, "Id");
            }
            else
            {
                if (Id.Length > PackageIdValidator.MaxPackageIdLength)
                {
                    yield return String.Format(CultureInfo.CurrentCulture, NuGetResources.Manifest_IdMaxLengthExceeded);
                }
                else if (!PackageIdValidator.IsValidPackageId(Id))
                {
                    yield return String.Format(CultureInfo.CurrentCulture, NuGetResources.InvalidPackageId, Id);
                }
            }

            if (Version == null)
            {
                yield return String.Format(CultureInfo.CurrentCulture, NuGetResources.Manifest_RequiredMetadataMissing, "Version");
            }

            if (Authors == null || !Authors.Any())
            {
                yield return String.Format(CultureInfo.CurrentCulture, NuGetResources.Manifest_RequiredMetadataMissing, "Authors");
            }

            if (String.IsNullOrEmpty(Description))
            {
                yield return String.Format(CultureInfo.CurrentCulture, NuGetResources.Manifest_RequiredMetadataMissing, "Description");
            }

            if (_licenseUrl == String.Empty)
            {
                yield return String.Format(CultureInfo.CurrentCulture, NuGetResources.Manifest_UriCannotBeEmpty, "LicenseUrl");
            }

            if (_iconUrl == String.Empty)
            {
                yield return String.Format(CultureInfo.CurrentCulture, NuGetResources.Manifest_UriCannotBeEmpty, "IconUrl");
            }

            if (_projectUrl == String.Empty)
            {
                yield return String.Format(CultureInfo.CurrentCulture, NuGetResources.Manifest_UriCannotBeEmpty, "ProjectUrl");
            }

            if (RequireLicenseAcceptance && String.IsNullOrWhiteSpace(_licenseUrl))
            {
                yield return NuGetResources.Manifest_RequireLicenseAcceptanceRequiresLicenseUrl;
            }
        }
    }
}