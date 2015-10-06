// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Frameworks;
using NuGet.Packaging.Core;

namespace NuGet.Packaging
{
    /// <summary>
    /// Reads an unzipped nupkg folder.
    /// </summary>
    public class PackageFolderReader : PackageReaderBase
    {
        private readonly DirectoryInfo _root;

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
            var nuspecFiles = _root.GetFiles("*.nuspec", SearchOption.TopDirectoryOnly);

            if (nuspecFiles.Length == 0)
            {
                throw new PackagingException(Strings.MissingNuspec);
            }
            else if (nuspecFiles.Length > 1)
            {
                throw new PackagingException(Strings.MultipleNuspecFiles);
            }

            return nuspecFiles[0].OpenRead();
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
            var searchFolder = new DirectoryInfo(_root.FullName);

            foreach (var file in searchFolder.GetFiles("*", SearchOption.AllDirectories).
                Where(p => !p.FullName.EndsWith(PackagingCoreConstants.NupkgExtension, StringComparison.OrdinalIgnoreCase)))
            {
                yield return GetRelativePath(_root, file);
            }

            yield break;
        }

        protected override IEnumerable<string> GetFiles(string folder)
        {
            var searchFolder = new DirectoryInfo(Path.Combine(_root.FullName, folder));

            if (searchFolder.Exists)
            {
                foreach (var file in searchFolder.GetFiles("*", SearchOption.AllDirectories).
                    Where(p => !p.FullName.EndsWith(PackagingCoreConstants.NupkgExtension, StringComparison.OrdinalIgnoreCase)))
                {
                    yield return GetRelativePath(_root, file);
                }
            }

            yield break;
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

            return String.Join("/", parts);
        }
    }
}
