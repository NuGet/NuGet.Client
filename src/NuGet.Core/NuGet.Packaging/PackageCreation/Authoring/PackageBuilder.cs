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

namespace NuGet.Packaging
{
    public class PackageBuilder : IPackageMetadata
    {
        private const string DefaultContentType = "application/octet";
        internal const string ManifestRelationType = "manifest";
        private readonly bool _includeEmptyDirectories;
        
        public PackageBuilder(string path, Func<string, string> propertyProvider, bool includeEmptyDirectories)
            : this(path, Path.GetDirectoryName(path), propertyProvider, includeEmptyDirectories)
        {
        }

        public PackageBuilder(string path, string basePath, Func<string, string> propertyProvider, bool includeEmptyDirectories)
            : this(includeEmptyDirectories)
        {
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
            ContentFiles = new Collection<ManifestContentFiles>();
            PackageAssemblyReferences = new Collection<PackageReferenceSet>();
            PackageTypes = new Collection<PackageType>();
            Authors = new HashSet<string>();
            Owners = new HashSet<string>();
            Tags = new HashSet<string>();
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

        public ISet<string> Tags
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
            if (!Files.Any() && !DependencyGroups.SelectMany(d => d.Packages).Any() && !FrameworkReferences.Any())
            {
                throw new InvalidOperationException(NuGetResources.CannotCreateEmptyPackage);
            }

            if (!ValidateSpecialVersionLength(Version))
            {
                throw new InvalidOperationException(NuGetResources.SemVerSpecialVersionTooLong);
            }

            if (Version != null && Version.IsSemVer2)
            {
                throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, NuGetResources.SemVer2VersionsNotSupported, Version));
            }

            ValidateDependencyGroups(Version, DependencyGroups);
            ValidateReferenceAssemblies(Files, PackageAssemblyReferences);

            using (var package = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                string psmdcpPath = $"package/services/metadata/core-properties/{Guid.NewGuid().ToString("N")}.psmdcp";

                // Validate and write the manifest
                WriteManifest(package, DetermineMinimumSchemaVersion(Files, DependencyGroups), psmdcpPath);

                // Write the files to the package
                var extensions = WriteFiles(package);

                extensions.Add("nuspec");

                WriteOpcContentTypes(package, extensions);

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

        public static void ValidateDependencyGroups(SemanticVersion version, IEnumerable<PackageDependencyGroup> dependencies)
        {
            if (version == null)
            {
                // We have independent validation for null-versions.
                return;
            }

            foreach (var dep in dependencies.SelectMany(s => s.Packages))
            {
                PackageIdValidator.ValidatePackageId(dep.Id);

                if (dep.VersionRange != null)
                {
                    if (dep.VersionRange.HasLowerBound && dep.VersionRange.MinVersion.IsSemVer2)
                    {
                        throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, NuGetResources.SemVer2VersionsNotSupported, dep.VersionRange.MinVersion));
                    }

                    if (dep.VersionRange.HasUpperBound && dep.VersionRange.MaxVersion.IsSemVer2)
                    {
                        throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, NuGetResources.SemVer2VersionsNotSupported, dep.VersionRange.MaxVersion));
                    }
                }
            }

            if (!version.IsPrerelease)
            {
                // If we are creating a production package, do not allow any of the dependencies to be a prerelease version.
                var prereleaseDependency = dependencies.SelectMany(set => set.Packages).FirstOrDefault(IsPrereleaseDependency);
                if (prereleaseDependency != null)
                {
                    throw new InvalidDataException(String.Format(CultureInfo.CurrentCulture, NuGetResources.Manifest_InvalidPrereleaseDependency, prereleaseDependency.ToString()));
                }
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
                    throw new InvalidDataException(String.Format(CultureInfo.CurrentCulture, NuGetResources.Manifest_InvalidReference, reference));
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
            Description = metadata.Description;
            Summary = metadata.Summary;
            ReleaseNotes = metadata.ReleaseNotes;
            Language = metadata.Language;
            Copyright = metadata.Copyright;
            MinClientVersion = metadata.MinClientVersion;
            ContentFiles = new Collection<ManifestContentFiles>(manifestMetadata.ContentFiles.ToList());

            if (metadata.Tags != null)
            {
                Tags.AddRange(ParseTags(metadata.Tags));
            }

            DependencyGroups.AddRange(metadata.DependencyGroups);
            FrameworkReferences.AddRange(metadata.FrameworkReferences);

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
            string path = Id + PackagingConstants.ManifestExtension;

            WriteOpcManifestRelationship(package, path, psmdcpPath);

            ZipArchiveEntry entry = package.CreateEntry(path, CompressionLevel.Optimal);

            using (Stream stream = entry.Open())
            {
                Manifest manifest = Manifest.Create(this);
                manifest.Save(stream, minimumManifestVersion);
            }
        }

        private HashSet<string> WriteFiles(ZipArchive package)
        {
            var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Add files that might not come from expanding files on disk
            foreach (IPackageFile file in new HashSet<IPackageFile>(Files))
            {
                using (Stream stream = file.GetStream())
                {
                    try
                    {
                        CreatePart(package, file.Path, stream);
                        var fileExtension = Path.GetExtension(file.Path);

                        // We have files without extension (e.g. the executables for Nix)
                        if (!string.IsNullOrEmpty(fileExtension))
                        {
                            extensions.Add(fileExtension.Substring(1));
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
            List<PhysicalPackageFile> searchFiles = ResolveSearchPattern(basePath, source.Replace('\\', Path.DirectorySeparatorChar), destination, _includeEmptyDirectories).ToList();
            if (_includeEmptyDirectories)
            {
                // we only allow empty directories which are under known root folders.
                searchFiles.RemoveAll(file => Path.GetFileName(file.TargetPath) == PackagingConstants.PackageEmptyFileName
                                             && !IsKnownFolder(file.TargetPath));
            }

            ExcludeFiles(searchFiles, basePath, exclude);

            if (!PathResolver.IsWildcardSearch(source) && !PathResolver.IsDirectoryPath(source) && !searchFiles.Any())
            {
                throw new FileNotFoundException(
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

        private static void CreatePart(ZipArchive package, string path, Stream sourceStream)
        {
            if (PackageHelper.IsNuspec(path) || ProjectJsonPathUtilities.IsProjectConfig(path))
            {
                return;
            }

            string entryName = CreatePartEntryName(path);

            var entry = package.CreateEntry(entryName, CompressionLevel.Optimal);
            using (var stream = entry.Open())
            {
                sourceStream.CopyTo(stream);
            }
        }

        internal static string CreatePartEntryName(string path)
        {
            // Only the segments between the path separators should be escaped
            var segments = path.Split(new[] { '/', Path.DirectorySeparatorChar }, StringSplitOptions.None)
                               .Select(Uri.EscapeDataString);
            return String.Join("/", segments);
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

        private static bool IsPrereleaseDependency(PackageDependency dependency)
        {
            return dependency.VersionRange.MinVersion?.IsPrerelease == true ||
                   dependency.VersionRange.MaxVersion?.IsPrerelease == true;
        }

        private static bool ValidateSpecialVersionLength(SemanticVersion version)
        {
            return version == null || !version.IsPrerelease || version.Release.Length <= 20;
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

        private static void WriteOpcContentTypes(ZipArchive package, HashSet<string> extensions)
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
            XNamespace core ="http://schemas.openxmlformats.org/package/2006/metadata/core-properties";

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