// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging.PackageCreation.Resources;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using NuGet.Packaging.Rules;
using System.Reflection.Emit;

namespace NuGet.Packaging
{
    public class PackageBuilder : IPackageMetadata
    {
        private const string DefaultContentType = "application/octet";
        private static readonly Uri DefaultUri = new Uri("http://defaultcontainer/");
        internal const string ManifestRelationType = "manifest";
        private readonly bool _includeEmptyDirectories;

        public PackageBuilder(string path, Func<string, string> propertyProvider, bool includeEmptyDirectories)
            : this(path, Path.GetDirectoryName(path), propertyProvider, includeEmptyDirectories)
        {
        }

        public PackageBuilder(string path, string basePath, Func<string, string> propertyProvider, bool includeEmptyDirectories)
            : this(includeEmptyDirectories)
        {
            if (!File.Exists(path))
            {
                throw new PackagingException(
                    NuGetLogCode.NU5008,
                    string.Format(CultureInfo.CurrentCulture, Strings.ErrorManifestFileNotFound, path ?? "null"));
            }

            using (Stream stream = File.OpenRead(path))
            {
                ReadManifest(stream, basePath, propertyProvider);
            }
        }

        public PackageBuilder(Stream stream, string basePath)
            : this(stream, basePath, null)
        {
        }

        public PackageBuilder(Stream stream, string basePath, Func<string, string> propertyProvider)
            : this()
        {
            ReadManifest(stream, basePath, propertyProvider);
        }

        public PackageBuilder()
            : this(includeEmptyDirectories: false)
        {
        }

        private PackageBuilder(bool includeEmptyDirectories)
        {
            _includeEmptyDirectories = includeEmptyDirectories;
            Files = new Collection<IPackageFile>();
            DependencyGroups = new Collection<PackageDependencyGroup>();
            FrameworkReferences = new Collection<FrameworkAssemblyReference>();
            FrameworkReferenceGroups = new Collection<FrameworkReferenceGroup>();
            ContentFiles = new Collection<ManifestContentFiles>();
            PackageAssemblyReferences = new Collection<PackageReferenceSet>();
            PackageTypes = new Collection<PackageType>();
            Authors = new HashSet<string>();
            Owners = new HashSet<string>();
            Tags = new HashSet<string>();
            TargetFrameworks = new List<NuGetFramework>();
            // Just like parameter replacements, these are also case insensitive, for consistency.
            Properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public string Id
        {
            get;
            set;
        }

        public NuGetVersion Version
        {
            get;
            set;
        }

        public RepositoryMetadata Repository { get; set; }

        public LicenseMetadata LicenseMetadata { get; set; }

        public bool HasSnapshotVersion
        {
            get;
            set;
        }

        public string Title
        {
            get;
            set;
        }

        public ISet<string> Authors
        {
            get;
            private set;
        }

        public ISet<string> Owners
        {
            get;
            private set;
        }

        public Uri IconUrl
        {
            get;
            set;
        }

        public string Icon
        {
            get;
            set;
        }

        public Uri LicenseUrl
        {
            get;
            set;
        }

        public Uri ProjectUrl
        {
            get;
            set;
        }

        public bool RequireLicenseAcceptance
        {
            get;
            set;
        }

        public bool Serviceable
        {
            get;
            set;
        }

        public bool DevelopmentDependency
        {
            get;
            set;
        }

        public string Description
        {
            get;
            set;
        }

        public string Summary
        {
            get;
            set;
        }

        public string ReleaseNotes
        {
            get;
            set;
        }

        public string Language
        {
            get;
            set;
        }

        public string OutputName
        {
            get;
            set;
        }

        public ISet<string> Tags
        {
            get;
            private set;
        }

        /// <summary>
        /// Exposes the additional properties extracted by the metadata 
        /// extractor or received from the command line.
        /// </summary>
        public Dictionary<string, string> Properties
        {
            get;
            private set;
        }

        public string Copyright
        {
            get;
            set;
        }

        public Collection<PackageDependencyGroup> DependencyGroups
        {
            get;
            private set;
        }

        public ICollection<IPackageFile> Files
        {
            get;
            private set;
        }

        public Collection<FrameworkAssemblyReference> FrameworkReferences
        {
            get;
            private set;
        }

        public Collection<FrameworkReferenceGroup> FrameworkReferenceGroups
        {
            get;
            private set;
        }

        public IList<NuGetFramework> TargetFrameworks
        {
            get;
            set;
        }

        /// <summary>
        /// ContentFiles section from the manifest for content v2
        /// </summary>
        public ICollection<ManifestContentFiles> ContentFiles
        {
            get;
            private set;
        }

        public ICollection<PackageReferenceSet> PackageAssemblyReferences
        {
            get;
            set;
        }

        public ICollection<PackageType> PackageTypes
        {
            get;
            set;
        }

        IEnumerable<string> IPackageMetadata.Authors
        {
            get
            {
                return Authors;
            }
        }

        IEnumerable<string> IPackageMetadata.Owners
        {
            get
            {
                return Owners;
            }
        }

        string IPackageMetadata.Tags
        {
            get
            {
                return String.Join(" ", Tags);
            }
        }

        IEnumerable<PackageReferenceSet> IPackageMetadata.PackageAssemblyReferences
        {
            get
            {
                return PackageAssemblyReferences;
            }
        }

        IEnumerable<PackageDependencyGroup> IPackageMetadata.DependencyGroups
        {
            get
            {
                return DependencyGroups;
            }
        }

        IEnumerable<FrameworkAssemblyReference> IPackageMetadata.FrameworkReferences
        {
            get
            {
                return FrameworkReferences;
            }
        }

        IEnumerable<ManifestContentFiles> IPackageMetadata.ContentFiles
        {
            get
            {
                return ContentFiles;
            }
        }

        IEnumerable<PackageType> IPackageMetadata.PackageTypes
        {
            get
            {
                return PackageTypes;
            }
        }

        IEnumerable<FrameworkReferenceGroup> IPackageMetadata.FrameworkReferenceGroups => FrameworkReferenceGroups;

        public Version MinClientVersion
        {
            get;
            set;
        }

        public void Save(Stream stream)
        {
            // Make sure we're saving a valid package id
            PackageIdValidator.ValidatePackageId(Id);

            // Throw if the package doesn't contain any dependencies nor content
            if (!Files.Any() && !DependencyGroups.SelectMany(d => d.Packages).Any() && !FrameworkReferences.Any() && !FrameworkReferenceGroups.Any())
            {
                throw new PackagingException(NuGetLogCode.NU5017, NuGetResources.CannotCreateEmptyPackage);
            }

            ValidateDependencies(Version, DependencyGroups);
            ValidateReferenceAssemblies(Files, PackageAssemblyReferences);
            ValidateLicenseFile(Files, LicenseMetadata);
            ValidateIconFile(Files, Icon);

            using (var package = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                string psmdcpPath = $"package/services/metadata/core-properties/{Guid.NewGuid().ToString("N")}.psmdcp";

                // Validate and write the manifest
                WriteManifest(package, DetermineMinimumSchemaVersion(Files, DependencyGroups), psmdcpPath);

                // Write the files to the package
                HashSet<string> filesWithoutExtensions = new HashSet<string>();
                var extensions = WriteFiles(package, filesWithoutExtensions);

                extensions.Add("nuspec");

                WriteOpcContentTypes(package, extensions, filesWithoutExtensions);

                WriteOpcPackageProperties(package, psmdcpPath);
            }
        }

        private static string CreatorInfo()
        {
            List<string> creatorInfo = new List<string>();
            var assembly = typeof(PackageBuilder).GetTypeInfo().Assembly;
            creatorInfo.Add(assembly.FullName);
#if !IS_CORECLR // CORECLR_TODO: Environment.OSVersion
            creatorInfo.Add(Environment.OSVersion.ToString());
#endif

            var attribute = assembly.GetCustomAttributes<System.Runtime.Versioning.TargetFrameworkAttribute>().FirstOrDefault();
            if (attribute != null)
            {
                creatorInfo.Add(attribute.FrameworkDisplayName);
            }

            return String.Join(";", creatorInfo);
        }

        private static int DetermineMinimumSchemaVersion(
            ICollection<IPackageFile> Files,
            ICollection<PackageDependencyGroup> package)
        {
            if (HasContentFilesV2(Files) || HasIncludeExclude(package))
            {
                // version 5
                return ManifestVersionUtility.XdtTransformationVersion;
            }

            if (HasXdtTransformFile(Files))
            {
                // version 5
                return ManifestVersionUtility.XdtTransformationVersion;
            }

            if (RequiresV4TargetFrameworkSchema(Files))
            {
                // version 4
                return ManifestVersionUtility.TargetFrameworkSupportForDependencyContentsAndToolsVersion;
            }

            return ManifestVersionUtility.DefaultVersion;
        }

        private static bool RequiresV4TargetFrameworkSchema(ICollection<IPackageFile> files)
        {
            // check if any file under Content or Tools has TargetFramework defined
            bool hasContentOrTool = files.Any(
                f => f.TargetFramework != null &&
                     f.TargetFramework.Identifier != FrameworkConstants.SpecialIdentifiers.Unsupported &&
                     (f.Path.StartsWith(PackagingConstants.Folders.Content + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                      f.Path.StartsWith(PackagingConstants.Folders.Tools + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)));

            if (hasContentOrTool)
            {
                return true;
            }

            // now check if the Lib folder has any empty framework folder
            bool hasEmptyLibFolder = files.Any(
                f => f.TargetFramework != null &&
                     f.Path.StartsWith(PackagingConstants.Folders.Lib + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                     f.EffectivePath == PackagingConstants.PackageEmptyFileName);

            return hasEmptyLibFolder;
        }

        private static bool HasContentFilesV2(ICollection<IPackageFile> contentFiles)
        {
            return contentFiles.Any(file =>
                file.Path != null &&
                file.Path.StartsWith(PackagingConstants.Folders.ContentFiles + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
        }

        private static bool HasIncludeExclude(IEnumerable<PackageDependencyGroup> dependencyGroups)
        {
            return dependencyGroups.Any(dependencyGroup =>
                dependencyGroup.Packages
                   .Any(dependency => dependency.Include != null || dependency.Exclude != null));
        }

        private static bool HasXdtTransformFile(ICollection<IPackageFile> contentFiles)
        {
            return contentFiles.Any(file =>
                file.Path != null &&
                file.Path.StartsWith(PackagingConstants.Folders.Content + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                (file.Path.EndsWith(".install.xdt", StringComparison.OrdinalIgnoreCase) ||
                 file.Path.EndsWith(".uninstall.xdt", StringComparison.OrdinalIgnoreCase)));
        }

        private static void ValidateDependencies(SemanticVersion version,
            IEnumerable<PackageDependencyGroup> dependencies)
        {
            if (version == null)
            {
                // We have independent validation for null-versions.
                return;
            }

            foreach (var dep in dependencies.SelectMany(s => s.Packages))
            {
                PackageIdValidator.ValidatePackageId(dep.Id);
            }
        }

        public static void ValidateReferenceAssemblies(IEnumerable<IPackageFile> files, IEnumerable<PackageReferenceSet> packageAssemblyReferences)
        {
            var libFiles = new HashSet<string>(from file in files
                                               where !String.IsNullOrEmpty(file.Path) && file.Path.StartsWith("lib", StringComparison.OrdinalIgnoreCase)
                                               select Path.GetFileName(file.Path), StringComparer.OrdinalIgnoreCase);

            foreach (var reference in packageAssemblyReferences.SelectMany(p => p.References))
            {
                if (!libFiles.Contains(reference) &&
                    !libFiles.Contains(reference + ".dll") &&
                    !libFiles.Contains(reference + ".exe") &&
                    !libFiles.Contains(reference + ".winmd"))
                {
                    throw new PackagingException(NuGetLogCode.NU5018, String.Format(CultureInfo.CurrentCulture, NuGetResources.Manifest_InvalidReference, reference));
                }
            }
        }

        private void ValidateLicenseFile(IEnumerable<IPackageFile> files, LicenseMetadata licenseMetadata)
        {
            if (!PackageTypes.Contains(PackageType.SymbolsPackage) && licenseMetadata?.Type == LicenseType.File)
            {
                var ext = Path.GetExtension(licenseMetadata.License);
                if (!string.IsNullOrEmpty(ext) &&
                        !ext.Equals(".txt", StringComparison.OrdinalIgnoreCase) &&
                        !ext.Equals(".md", StringComparison.OrdinalIgnoreCase))
                {
                    throw new PackagingException(NuGetLogCode.NU5031, string.Format(CultureInfo.CurrentCulture, NuGetResources.Manifest_LicenseFileExtensionIsInvalid, licenseMetadata.License));
                }
                var strippedLicenseFileLocation = PathUtility.StripLeadingDirectorySeparators(licenseMetadata.License);
                var count = files.Where(e => PathUtility.StripLeadingDirectorySeparators(e.Path).Equals(strippedLicenseFileLocation, PathUtility.GetStringComparisonBasedOnOS())).Count();
                if (count == 0)
                {
                    throw new PackagingException(NuGetLogCode.NU5030, string.Format(CultureInfo.CurrentCulture, NuGetResources.Manifest_LicenseFileIsNotInNupkg, licenseMetadata.License));
                }
            }
        }

        /// <summary>
        /// Given a list of resolved files,
        /// determine which file will be used as the icon file and validate its size.
        /// </summary>
        /// <param name="files">Files resolved from the file entries in the nuspec</param>
        /// <param name="iconPath">iconpath found in the .nuspec</param>
        /// <exception cref="PackagingException">When a validation rule is not met</exception>
        private void ValidateIconFile(IEnumerable<IPackageFile> files, string iconPath)
        {
            if (!string.IsNullOrEmpty(iconPath))
            {
                // Validate entry
                var iconPathStripped = PathUtility.StripLeadingDirectorySeparators(iconPath);

                var iconFileList = files.Where(f =>
                        iconPath.Equals(
                            PathUtility.StripLeadingDirectorySeparators(f.Path),
                            PathUtility.GetStringComparisonBasedOnOS()));

                if (iconFileList.Count() > 1)
                {
                    throw new PackagingException(
                        NuGetLogCode.NU5038,
                        string.Format(CultureInfo.CurrentCulture, NuGetResources.IconMultipleIconFiles, string.Join(",", iconFileList)));
                }

                if (iconFileList.Count() == 0)
                {
                    throw new PackagingException(NuGetLogCode.NU5039, NuGetResources.IconNoFileElement);
                }

                IPackageFile iconFile = iconFileList.First();

                try
                {
                    // Validate Icon open file
                    using (var iconStream = iconFile.GetStream())
                    {
                        // Validate file size
                        IconValidation.ValidateIconFileSize(iconStream);
                    }
                }
                catch (FileNotFoundException e)
                {
                    throw new PackagingException(
                        NuGetLogCode.NU5036,
                        string.Format(CultureInfo.CurrentCulture, NuGetResources.IconCannotOpenFile, iconPath, e.Message));
                }
            }
        }

        private void ReadManifest(Stream stream, string basePath, Func<string, string> propertyProvider)
        {
            // Deserialize the document and extract the metadata
            Manifest manifest = Manifest.ReadFrom(stream, propertyProvider, validateSchema: true);

            Populate(manifest.Metadata);

            // If there's no base path then ignore the files node
            if (basePath != null)
            {
                if (!manifest.HasFilesNode)
                {
                    AddFiles(basePath, @"**\*", null);
                }
                else
                {
                    PopulateFiles(basePath, manifest.Files);
                }
            }
        }

        public void Populate(ManifestMetadata manifestMetadata)
        {
            IPackageMetadata metadata = manifestMetadata;
            Id = metadata.Id;
            Version = metadata.Version;
            Title = metadata.Title;
            Authors.AddRange(metadata.Authors);
            Owners.AddRange(metadata.Owners);
            IconUrl = metadata.IconUrl;
            LicenseUrl = metadata.LicenseUrl;
            ProjectUrl = metadata.ProjectUrl;
            RequireLicenseAcceptance = metadata.RequireLicenseAcceptance;
            DevelopmentDependency = metadata.DevelopmentDependency;
            Serviceable = metadata.Serviceable;
            Description = metadata.Description;
            Summary = metadata.Summary;
            ReleaseNotes = metadata.ReleaseNotes;
            Language = metadata.Language;
            Copyright = metadata.Copyright;
            MinClientVersion = metadata.MinClientVersion;
            Repository = metadata.Repository;
            ContentFiles = new Collection<ManifestContentFiles>(manifestMetadata.ContentFiles.ToList());
            LicenseMetadata = metadata.LicenseMetadata;
            Icon = metadata.Icon;

            if (metadata.Tags != null)
            {
                Tags.AddRange(ParseTags(metadata.Tags));
            }

            DependencyGroups.AddRange(metadata.DependencyGroups);
            FrameworkReferences.AddRange(metadata.FrameworkReferences);
            FrameworkReferenceGroups.AddRange(metadata.FrameworkReferenceGroups);

            if (manifestMetadata.PackageAssemblyReferences != null)
            {
                PackageAssemblyReferences.AddRange(manifestMetadata.PackageAssemblyReferences);
            }

            if (manifestMetadata.PackageTypes != null)
            {
                PackageTypes = new Collection<PackageType>(metadata.PackageTypes.ToList());
            }
        }

        public void PopulateFiles(string basePath, IEnumerable<ManifestFile> files)
        {
            foreach (var file in files)
            {
                AddFiles(basePath, file.Source, file.Target, file.Exclude);
            }
        }

        private void WriteManifest(ZipArchive package, int minimumManifestVersion, string psmdcpPath)
        {
            var path = Id + PackagingConstants.ManifestExtension;

            WriteOpcManifestRelationship(package, path, psmdcpPath);

            var entry = package.CreateEntry(path, CompressionLevel.Optimal);

            using (var stream = entry.Open())
            {
                var manifest = Manifest.Create(this);
                manifest.Save(stream, minimumManifestVersion);
            }
        }

        private HashSet<string> WriteFiles(ZipArchive package, HashSet<string> filesWithoutExtensions)
        {
            var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Add files that might not come from expanding files on disk
            foreach (IPackageFile file in new HashSet<IPackageFile>(Files))
            {
                using (Stream stream = file.GetStream())
                {
                    try
                    {
                        CreatePart(package, file.Path, stream, file.LastWriteTime); 
                        var fileExtension = Path.GetExtension(file.Path);

                        // We have files without extension (e.g. the executables for Nix)
                        if (!string.IsNullOrEmpty(fileExtension))
                        {
                            extensions.Add(fileExtension.Substring(1));
                        }
                        else
                        {
                            filesWithoutExtensions.Add($"/{file.Path.Replace("\\", "/")}");
                        }
                    }
                    catch
                    {
                        throw;
                    }
                }
            }

            return extensions;
        }

        public void AddFiles(string basePath, string source, string destination, string exclude = null)
        {
            exclude = exclude?.Replace('\\', Path.DirectorySeparatorChar);

            List<PhysicalPackageFile> searchFiles = ResolveSearchPattern(basePath, source.Replace('\\', Path.DirectorySeparatorChar), destination, _includeEmptyDirectories).ToList();

            if (_includeEmptyDirectories)
            {
                // we only allow empty directories which are under known root folders.
                searchFiles.RemoveAll(file => Path.GetFileName(file.TargetPath) == PackagingConstants.PackageEmptyFileName
                                             && !IsKnownFolder(file.TargetPath));
            }

            ExcludeFiles(searchFiles, basePath, exclude);

            // Don't throw if the exclude is what made this find no files. Adding files from
            // project.json ends up calling this one file at a time where some may be filtered out.
            if (!PathResolver.IsWildcardSearch(source) && !PathResolver.IsDirectoryPath(source) && !searchFiles.Any() && string.IsNullOrEmpty(exclude))
            {
                throw new PackagingException(NuGetLogCode.NU5019,
                    String.Format(CultureInfo.CurrentCulture, NuGetResources.PackageAuthoring_FileNotFound, source));
            }

            Files.AddRange(searchFiles);
        }

        internal static IEnumerable<PhysicalPackageFile> ResolveSearchPattern(string basePath, string searchPath, string targetPath, bool includeEmptyDirectories)
        {
            string normalizedBasePath;
            IEnumerable<PathResolver.SearchPathResult> searchResults = PathResolver.PerformWildcardSearch(basePath, searchPath, includeEmptyDirectories, out normalizedBasePath);

            return searchResults.Select(result =>
                result.IsFile
                    ? new PhysicalPackageFile
                    {
                        SourcePath = result.Path,
                        TargetPath = ResolvePackagePath(normalizedBasePath, searchPath, result.Path, targetPath)
                    }
                    : new EmptyFrameworkFolderFile(ResolvePackagePath(normalizedBasePath, searchPath, result.Path, targetPath))
                    {
                        SourcePath = result.Path
                    }
            );
        }

        /// <summary>
        /// Determins the path of the file inside a package.
        /// For recursive wildcard paths, we preserve the path portion beginning with the wildcard.
        /// For non-recursive wildcard paths, we use the file name from the actual file path on disk.
        /// </summary>
        internal static string ResolvePackagePath(string searchDirectory, string searchPattern, string fullPath, string targetPath)
        {
            string packagePath;
            bool isDirectorySearch = PathResolver.IsDirectoryPath(searchPattern);
            bool isWildcardSearch = PathResolver.IsWildcardSearch(searchPattern);
            bool isRecursiveWildcardSearch = isWildcardSearch && searchPattern.IndexOf("**", StringComparison.OrdinalIgnoreCase) != -1;

            if ((isRecursiveWildcardSearch || isDirectorySearch) && fullPath.StartsWith(searchDirectory, StringComparison.OrdinalIgnoreCase))
            {
                // The search pattern is recursive. Preserve the non-wildcard portion of the path.
                // e.g. Search: X:\foo\**\*.cs results in SearchDirectory: X:\foo and a file path of X:\foo\bar\biz\boz.cs
                // Truncating X:\foo\ would result in the package path.
                packagePath = fullPath.Substring(searchDirectory.Length).TrimStart(Path.DirectorySeparatorChar);
            }
            else if (!isWildcardSearch && Path.GetExtension(searchPattern).Equals(Path.GetExtension(targetPath), StringComparison.OrdinalIgnoreCase))
            {
                // If the search does not contain wild cards, and the target path shares the same extension, copy it
                // e.g. <file src="ie\css\style.css" target="Content\css\ie.css" /> --> Content\css\ie.css
                return targetPath;
            }
            else
            {
                packagePath = Path.GetFileName(fullPath);
            }
            return Path.Combine(targetPath ?? String.Empty, packagePath);
        }

        /// <summary>
        /// Returns true if the path uses a known folder root.
        /// </summary>
        private static bool IsKnownFolder(string targetPath)
        {
            if (targetPath != null)
            {
                var parts = targetPath.Split(
                    new char[] { '\\', '/' },
                    StringSplitOptions.RemoveEmptyEntries);

                // exclude things in the root of the directory, this is not allowed
                // for any of the v3 folders.
                // example: an empty 'native' folder does not have a TxM and cannot be used.
                if (parts.Length > 1)
                {
                    var topLevelDirectory = parts.FirstOrDefault();

                    return PackagingConstants.Folders.Known.Any(folder =>
                        folder.Equals(topLevelDirectory, StringComparison.OrdinalIgnoreCase));
                }
            }

            return false;
        }

        private static void ExcludeFiles(List<PhysicalPackageFile> searchFiles, string basePath, string exclude)
        {
            if (String.IsNullOrEmpty(exclude))
            {
                return;
            }

            // One or more exclusions may be specified in the file. Split it and prepend the base path to the wildcard provided.
            var exclusions = exclude.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var item in exclusions)
            {
                string wildCard = PathResolver.NormalizeWildcardForExcludedFiles(basePath, item);
                PathResolver.FilterPackageFiles(searchFiles, p => p.SourcePath, new[] { wildCard });
            }
        }

        private static void CreatePart(ZipArchive package, string path, Stream sourceStream, DateTimeOffset lastWriteTime)
        {
            if (PackageHelper.IsNuspec(path))
            {
                return;
            }

            string entryName = CreatePartEntryName(path);
            var entry = package.CreateEntry(entryName, CompressionLevel.Optimal);
            entry.LastWriteTime = lastWriteTime;
            using (var stream = entry.Open())
            {
                sourceStream.CopyTo(stream);
            }
        }

        internal static string CreatePartEntryName(string path)
        {
            // Only the segments between the path separators should be escaped
            var segments = path.Split(new[] { '/', '\\', Path.DirectorySeparatorChar }, StringSplitOptions.None)
                .Select(Uri.EscapeDataString);

            var escapedPath = String.Join("/", segments);

            // retrieve only relative path with resolved . or ..
            return GetStringForPartUri(escapedPath);
        }

        internal static string GetStringForPartUri(string escapedPath)
        {
            //Create an absolute URI to get the refinement on the relative path
            var partUri = new Uri(DefaultUri, escapedPath);

            // Get the safe-unescaped form of the URI first. This will unescape all the characters
            Uri safeUnescapedUri = new Uri(partUri.GetComponents(UriComponents.Path, UriFormat.SafeUnescaped), UriKind.Relative);

            return safeUnescapedUri.GetComponents(UriComponents.SerializationInfoString, UriFormat.Unescaped);
        }

        /// <summary>
        /// Tags come in this format. tag1 tag2 tag3 etc..
        /// </summary>
        private static IEnumerable<string> ParseTags(string tags)
        {
            Debug.Assert(tags != null);
            return from tag in tags.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                   select tag.Trim();
        }

        private void WriteOpcManifestRelationship(ZipArchive package, string path, string psmdcpPath)
        {
            ZipArchiveEntry relsEntry = package.CreateEntry("_rels/.rels", CompressionLevel.Optimal);

            XNamespace relationships = "http://schemas.openxmlformats.org/package/2006/relationships";

            XDocument document = new XDocument(
                new XElement(relationships + "Relationships",
                    new XElement(relationships + "Relationship",
                        new XAttribute("Type", "http://schemas.microsoft.com/packaging/2010/07/manifest"),
                        new XAttribute("Target", $"/{path}"),
                        new XAttribute("Id", GenerateRelationshipId())),
                    new XElement(relationships + "Relationship",
                        new XAttribute("Type", "http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties"),
                        new XAttribute("Target", $"/{psmdcpPath}"),
                        new XAttribute("Id", GenerateRelationshipId()))
                    )
                );

            using (var writer = new StreamWriter(relsEntry.Open()))
            {
                document.Save(writer);
                writer.Flush();
            }
        }

        private static void WriteOpcContentTypes(ZipArchive package, HashSet<string> extensions, HashSet<string> filesWithoutExtensions)
        {
            // OPC backwards compatibility
            ZipArchiveEntry relsEntry = package.CreateEntry("[Content_Types].xml", CompressionLevel.Optimal);

            XNamespace content = "http://schemas.openxmlformats.org/package/2006/content-types";
            XElement element = new XElement(content + "Types",
                new XElement(content + "Default",
                    new XAttribute("Extension", "rels"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                new XElement(content + "Default",
                    new XAttribute("Extension", "psmdcp"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-package.core-properties+xml"))
                    );
            foreach (var extension in extensions)
            {
                element.Add(
                    new XElement(content + "Default",
                        new XAttribute("Extension", extension),
                        new XAttribute("ContentType", "application/octet")
                        )
                    );
            }
            foreach (var file in filesWithoutExtensions)
            {
                element.Add(
                    new XElement(content + "Override",
                        new XAttribute("PartName", file),
                        new XAttribute("ContentType", "application/octet")
                        )
                    );
            }

            XDocument document = new XDocument(element);

            using (var writer = new StreamWriter(relsEntry.Open()))
            {
                document.Save(writer);
                writer.Flush();
            }
        }

        // OPC backwards compatibility for package properties
        private void WriteOpcPackageProperties(ZipArchive package, string psmdcpPath)
        {
            ZipArchiveEntry packageEntry = package.CreateEntry(psmdcpPath, CompressionLevel.Optimal);

            var dcText = "http://purl.org/dc/elements/1.1/";
            XNamespace dc = dcText;
            var dctermsText = "http://purl.org/dc/terms/";
            XNamespace dcterms = dctermsText;
            var xsiText = "http://www.w3.org/2001/XMLSchema-instance";
            XNamespace xsi = xsiText;
            XNamespace core = "http://schemas.openxmlformats.org/package/2006/metadata/core-properties";

            XDocument document = new XDocument(
                new XElement(core + "coreProperties",
                    new XAttribute(XNamespace.Xmlns + "dc", dcText),
                    new XAttribute(XNamespace.Xmlns + "dcterms", dctermsText),
                    new XAttribute(XNamespace.Xmlns + "xsi", xsiText),
                    new XElement(dc + "creator", String.Join(", ", Authors)),
                    new XElement(dc + "description", Description),
                    new XElement(dc + "identifier", Id),
                    new XElement(core + "version", Version.ToString()),
                    //new XElement(core + "language", Language),
                    new XElement(core + "keywords", ((IPackageMetadata)this).Tags),
                    //new XElement(dc + "title", Title),
                    new XElement(core + "lastModifiedBy", CreatorInfo())
                    )
                );


            using (var writer = new StreamWriter(packageEntry.Open()))
            {
                document.Save(writer);
                writer.Flush();
            }
        }

        // Generate a relationship id for compatibility
        private string GenerateRelationshipId()
        {
            return "R" + Guid.NewGuid().ToString("N").Substring(0, 16);
        }
    }
}