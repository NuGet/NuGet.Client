// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NuGet.Frameworks;

namespace NuGet.Packaging
{
    /// <summary>
    /// Reads an unzipped nupkg folder.
    /// </summary>
    public class PackageFolderReader : PackageReaderBase
    {
        private const string PathSeparator = "/";
        private readonly DirectoryInfo _root;
        private List<string> _cachedPaths;
        private FileInfo _nuspecFileInfo;

        /// <summary>
        /// Package folder reader
        /// </summary>
        public PackageFolderReader(string folderPath)
            : this(folderPath, DefaultFrameworkNameProvider.Instance, DefaultCompatibilityProvider.Instance)
        {
        }

        /// <summary>
        /// Package folder reader
        /// </summary>
        /// <param name="folder">root directory of an extracted nupkg</param>
        public PackageFolderReader(DirectoryInfo folder)
            : this(folder, DefaultFrameworkNameProvider.Instance, DefaultCompatibilityProvider.Instance)
        {
        }

        /// <summary>
        /// Package folder reader
        /// </summary>
        /// <param name="folderPath">root directory of an extracted nupkg</param>
        /// <param name="frameworkProvider">framework mappings</param>
        /// <param name="compatibilityProvider">framework compatibility provider</param>
        public PackageFolderReader(string folderPath, IFrameworkNameProvider frameworkProvider, IFrameworkCompatibilityProvider compatibilityProvider)
            : this(new DirectoryInfo(folderPath), frameworkProvider, compatibilityProvider)
        {
        }

        /// <summary>
        /// Package folder reader
        /// </summary>
        /// <param name="folder">root directory of an extracted nupkg</param>
        /// <param name="frameworkProvider">framework mappings</param>
        /// <param name="compatibilityProvider">framework compatibility provider</param>
        public PackageFolderReader(DirectoryInfo folder, IFrameworkNameProvider frameworkProvider, IFrameworkCompatibilityProvider compatibilityProvider)
            : base(frameworkProvider, compatibilityProvider)
        {
            _root = folder;
        }

        /// <summary>
        /// Opens the nuspec file in read only mode.
        /// </summary>
        public override Stream GetNuspec()
        {
            if (_nuspecFileInfo == null)
            {
                _nuspecFileInfo = _root.EnumerateFiles("*.nuspec", SearchOption.TopDirectoryOnly).FirstOrDefault();
            }

            if (_nuspecFileInfo == null)
            {
                throw new FileNotFoundException(String.Format(CultureInfo.CurrentCulture, Strings.MissingNuspec, _root.FullName));
            }

            return _nuspecFileInfo.OpenRead();
        }

        /// <summary>
        /// Opens a local file in read only mode.
        /// </summary>
        public override Stream GetStream(string path)
        {
            var file = new FileInfo(Path.Combine(_root.FullName, path));

            if (!file.FullName.StartsWith(_root.FullName, StringComparison.OrdinalIgnoreCase))
            {
                // the given path does not appear under the folder root
                throw new FileNotFoundException(path);
            }

            return file.OpenRead();
        }

        public override IEnumerable<string> GetFiles()
        {
            EnsureFileCache();
            return _cachedPaths;
        }

        // TODO: add support for NuGet.ContentModel here
        protected override IEnumerable<string> GetFiles(string folder)
        {
            EnsureFileCache();
            return _cachedPaths.Where(
                path => path.StartsWith(folder + PathSeparator, StringComparison.OrdinalIgnoreCase));
        }

        private void EnsureFileCache()
        {
            if (_cachedPaths == null)
            {
                _cachedPaths = _root.EnumerateFiles("*", SearchOption.AllDirectories)
                    .Where(path => !path.Extension.Equals(".nupkg", StringComparison.OrdinalIgnoreCase))
                    .Select(path => GetRelativePath(_root, path))
                    .ToList();
            }
        }

        /// <summary>
        /// Build the relative path in the same format that ZipArchive uses
        /// </summary>
        private static string GetRelativePath(DirectoryInfo root, FileInfo file)
        {
            var parents = new Stack<DirectoryInfo>();

            var parent = file.Directory;

            while (parent != null
                   && !StringComparer.OrdinalIgnoreCase.Equals(parent.FullName, root.FullName))
            {
                parents.Push(parent);
                parent = parent.Parent;
            }

            if (parent == null)
            {
                // the given file path does not appear under root
                throw new FileNotFoundException(file.FullName);
            }

            var parts = parents.Select(d => d.Name).Concat(new string[] { file.Name });

            return String.Join(PathSeparator, parts);
        }
    }
}
