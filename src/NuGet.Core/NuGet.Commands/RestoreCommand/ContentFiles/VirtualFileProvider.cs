// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace NuGet.Commands
{
    /// <summary>
    /// A virtual file system based on a list of strings from ContentModel.
    /// </summary>
    internal class VirtualFileProvider : IFileProvider
    {
        public const string RootDir = "ROOT";
        private readonly string _originalFile;
        private string[] _splitFile;
        private const string ForwardSlash = "/";
        private const char ForwardSlashChar = '/';

        public VirtualFileProvider(string file)
        {
            _originalFile = file;
            _splitFile = null;
        }

        public IDirectoryContents GetDirectoryContents(string subpath)
        {
            var subPathParts = subpath.Split(ForwardSlashChar);

            // Remove the root identifier from the relative path
            var subPathPartsOffset = subPathParts.Length > 0 && string.Equals(subPathParts[0], RootDir, StringComparison.Ordinal) ? 1 : 0;
            _splitFile ??= _originalFile.Split(ForwardSlashChar);

            int i = 0;
            // Walk the path as long as both the file and subpath contain the same directories
            while (i < _splitFile.Length - 1
                && i + subPathPartsOffset < subPathParts.Length
                && string.Equals(_splitFile[i], subPathParts[i + subPathPartsOffset], StringComparison.OrdinalIgnoreCase))
            {
                i++;
            }

            IFileInfo fileInfo = null;
            // Check if the entire subpath was matched
            if (i + subPathPartsOffset == subPathParts.Length)
            {
                // All items are files. The last string in the array will be the file name.
                if (i == _splitFile.Length - 1)
                {
                    // File
                    var virtualFile = new VirtualFileInfo(_originalFile);
                    fileInfo = virtualFile;
                }
                else
                {
                    // Dir
                    var dirPath = string.Join(ForwardSlash, _splitFile.Take(i + 1));
                    var virtualDir = new VirtualFileInfo(dirPath, isDirectory: true);
                    fileInfo = virtualDir;
                }
            }

            return new EnumerableDirectoryContents(fileInfo);
        }

        public IFileInfo GetFileInfo(string subpath)
        {
            return new VirtualFileInfo(subpath);
        }

        public IChangeToken Watch(string filter)
        {
            return NullChangeToken.Singleton;
        }

        private class EnumerableDirectoryContents : IDirectoryContents
        {
            private readonly IFileInfo _entry;

            public EnumerableDirectoryContents(IFileInfo entry)
            {
                _entry = entry;
            }

            public bool Exists => true;

            public IEnumerator<IFileInfo> GetEnumerator() => new SingleFileInfoEnumerator(_entry);

            IEnumerator IEnumerable.GetEnumerator() => new SingleFileInfoEnumerator(_entry);
        }

        private struct SingleFileInfoEnumerator : IEnumerator<IFileInfo>
        {
            private IFileInfo _entry;
            private IFileInfo _current;
            private bool _done = false;

            public SingleFileInfoEnumerator(IFileInfo entry)
            {
                _current = null;
                _entry = entry;
                if (_entry is null)
                {
                    _done = true;
                }
            }

            public IFileInfo Current => _current;

            object IEnumerator.Current => _current;

            public void Dispose()
            {
                _done = true;
                _current = null;
                _entry = null;
            }

            public bool MoveNext()
            {
                if (_done)
                {
                    _current = null;

                    return false;
                }
                else
                {
                    _current = _entry;
                    _done = true;

                    return true;
                }
            }

            public void Reset()
            {
                _done = _entry is not null;
                _current = null;
            }
        }
    }
}
