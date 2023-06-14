// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace NuGet.Commands;

/// <summary>
/// A virtual file system based on a a single path string from ContentModel.
/// </summary>
internal sealed class SingleFileProvider : IFileProvider
{
    public const string RootPrefix = "ROOT";

    private readonly string _path;

    public SingleFileProvider(string path)
    {
        _path = path;
    }

    public IDirectoryContents GetDirectoryContents(string subpath)
    {
        if (subpath.Length == 0)
        {
            return EnumerableDirectoryContents.Empty;
        }

        ReadOnlySpan<char> span = subpath.AsSpan();

        // Remove the root identifier from the relative path
        if (span.StartsWith(RootPrefix.AsSpan(), StringComparison.Ordinal))
        {
            span = span.Slice(RootPrefix.Length);
        }

        span = span.TrimStart('/');

        if (_path.AsSpan().StartsWith(span, StringComparison.OrdinalIgnoreCase))
        {
            if (span.Length == _path.Length)
            {
                // Exact match
                return EnumerableDirectoryContents.Empty;
            }

            if (span.Length == 0 || _path[span.Length] == '/')
            {
                // Found an entry.
                int slashIndex = _path.IndexOf('/', span.Length + 1);

                if (slashIndex == -1)
                {
                    // This is a file.
                    return new SingleDirectoryContents(new VirtualFileInfo(_path, isDirectory: false));
                }
                else
                {
                    // This is a directory.
                    var path = _path.Substring(0, slashIndex);
                    return new SingleDirectoryContents(new VirtualFileInfo(path, isDirectory: true));
                }
            }
        }

        return EnumerableDirectoryContents.Empty;
    }

    public IFileInfo GetFileInfo(string subpath)
    {
        return new VirtualFileInfo(subpath);
    }

    public IChangeToken Watch(string filter)
    {
        return NullChangeToken.Singleton;
    }

    private class SingleDirectoryContents : IDirectoryContents
    {
        private readonly IFileInfo _fileInfo;

        public SingleDirectoryContents(IFileInfo fileInfo) => _fileInfo = fileInfo;

        public bool Exists => true;

        public IEnumerator<IFileInfo> GetEnumerator() { yield return _fileInfo; }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private class EnumerableDirectoryContents : IDirectoryContents
    {
        public static readonly EnumerableDirectoryContents Empty = new(Enumerable.Empty<IFileInfo>());

        private readonly IEnumerable<IFileInfo> _entries;

        private EnumerableDirectoryContents(IEnumerable<IFileInfo> entries) => _entries = entries;

        public bool Exists => true;

        public IEnumerator<IFileInfo> GetEnumerator() => _entries.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _entries.GetEnumerator();
    }
}
