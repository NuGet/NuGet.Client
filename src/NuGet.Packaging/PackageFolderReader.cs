// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace NuGet.Packaging
{
    /// <summary>
    /// Reads an unzipped nupkg folder.
    /// </summary>
    public class PackageFolderReader : PackageReaderBase
    {
        private readonly DirectoryInfo _root;

        public PackageFolderReader(string folderPath)
            : this(new DirectoryInfo(folderPath))
        {
        }

        public PackageFolderReader(DirectoryInfo folder)
        {
            _root = folder;
        }

        /// <summary>
        /// Opens the nuspec file in read only mode.
        /// </summary>
        public override Stream GetNuspec()
        {
            var nuspecFile = _root.GetFiles("*.nuspec", SearchOption.TopDirectoryOnly).FirstOrDefault();

            if (nuspecFile == null)
            {
                throw new FileNotFoundException(String.Format(CultureInfo.CurrentCulture, Strings.MissingNuspec, _root.FullName));
            }

            return nuspecFile.OpenRead();
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

            foreach (var file in searchFolder.GetFiles("*", SearchOption.AllDirectories))
            {
                yield return GetRelativePath(_root, file);
            }

            yield break;
        }

        // TODO: add support for NuGet.ContentModel here
        protected override IEnumerable<string> GetFiles(string folder)
        {
            var searchFolder = new DirectoryInfo(Path.Combine(_root.FullName, folder));

            if (searchFolder.Exists)
            {
                foreach (var file in searchFolder.GetFiles("*", SearchOption.AllDirectories))
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
