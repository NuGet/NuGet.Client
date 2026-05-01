// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace NuGet.Commands
{
    /// <summary>
    /// A lightweight <see cref="DirectoryInfoBase"/> that presents a single relative file path
    /// (e.g. "any/cs/net5.0/file.cs") as a virtual directory tree for use with
    /// <see cref="Microsoft.Extensions.FileSystemGlobbing.Matcher.Execute(DirectoryInfoBase)"/>.
    ///
    /// Splits the path on '/' into segments and lazily yields one child per level,
    /// with the final segment treated as a file. No filesystem calls are made.
    /// </summary>
    internal sealed class SingleFileDirectory : DirectoryInfoBase
    {
        private readonly string[] _segments;
        private readonly int _index; // -1 = root, 0..n-2 = directory segments

        internal SingleFileDirectory(string relativePath)
            : this(relativePath.Split('/'), -1)
        {
        }

        private SingleFileDirectory(string[] segments, int index)
        {
            _segments = segments;
            _index = index;
        }

        public override string Name => _index < 0 ? "." : _segments[_index];

        public override string FullName => _index < 0 ? "/" : string.Join("/", _segments, 0, _index + 1);

        public override DirectoryInfoBase ParentDirectory =>
            _index > 0 ? new SingleFileDirectory(_segments, _index - 1) : this;

        public override IEnumerable<FileSystemInfoBase> EnumerateFileSystemInfos()
        {
            int childIndex = _index + 1;
            if (childIndex < _segments.Length - 1)
            {
                yield return new SingleFileDirectory(_segments, childIndex);
            }
            else if (childIndex == _segments.Length - 1)
            {
                yield return new SingleFileInfo(_segments, this);
            }
        }

        public override DirectoryInfoBase GetDirectory(string path)
        {
            int childIndex = _index + 1;
            if (childIndex < _segments.Length - 1 &&
                string.Equals(_segments[childIndex], path, StringComparison.OrdinalIgnoreCase))
            {
                return new SingleFileDirectory(_segments, childIndex);
            }

            return new EmptyDirectory(path, this);
        }

        public override FileInfoBase? GetFile(string path) => null;

        private sealed class SingleFileInfo : FileInfoBase
        {
            private readonly string[] _segments;
            private readonly DirectoryInfoBase _parent;

            internal SingleFileInfo(string[] segments, DirectoryInfoBase parent)
            {
                _segments = segments;
                _parent = parent;
            }

            public override string Name => _segments[_segments.Length - 1];

            public override string FullName => string.Join("/", _segments);

            public override DirectoryInfoBase ParentDirectory => _parent;
        }

        private sealed class EmptyDirectory : DirectoryInfoBase
        {
            private readonly DirectoryInfoBase _parent;

            internal EmptyDirectory(string name, DirectoryInfoBase parent)
            {
                Name = name;
                _parent = parent;
            }

            public override string Name { get; }

            public override string FullName => Name;

            public override DirectoryInfoBase ParentDirectory => _parent;

            public override IEnumerable<FileSystemInfoBase> EnumerateFileSystemInfos()
            {
                return Array.Empty<FileSystemInfoBase>();
            }

            public override DirectoryInfoBase GetDirectory(string path)
            {
                return new EmptyDirectory(path, this);
            }

            public override FileInfoBase? GetFile(string path) => null;
        }
    }
}

