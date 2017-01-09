﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNet.FileProviders;
using Microsoft.Extensions.Primitives;

namespace NuGet.Commands
{
    /// <summary>
    /// A virtual file system based on a list of strings from ContentModel.
    /// </summary>
    internal class VirtualFileProvider : IFileProvider
    {
        public static readonly string RootDir = "ROOT";
        private readonly List<string[]> _files;
        private const string ForwardSlash = "/";
        private const char ForwardSlashChar = '/';

        public VirtualFileProvider(List<string> files)
        {
            _files = files.Select(file => file.Split(ForwardSlashChar)).ToList();
        }

        public IDirectoryContents GetDirectoryContents(string subpath)
        {
            var contents = new List<IFileInfo>();
            var subPathParts = subpath.Split(ForwardSlashChar);

            // Remove the root identifier from the relative path
            if (string.Equals(subPathParts.FirstOrDefault(), RootDir, StringComparison.Ordinal))
            {
                subPathParts = subPathParts.Skip(1).ToArray();
            }

            foreach (var file in _files)
            {
                var i = 0;

                // Walk the path as long as both the file and subpath contain the same directories
                while (i < file.Length - 1
                    && i < subPathParts.Length
                    && string.Equals(file[i], subPathParts[i], StringComparison.OrdinalIgnoreCase))
                {
                    i++;
                }

                // Check if the entire subpath was matched
                if (i == subPathParts.Length)
                {
                    // All items are files. The last string in the array will be the file name.
                    if (i == file.Length - 1)
                    {
                        // File
                        var virtualFile = new VirtualFileInfo(string.Join(ForwardSlash, file));
                        contents.Add(virtualFile);
                    }
                    else
                    {
                        // Dir
                        var dirPath = string.Join(ForwardSlash, file.Take(i + 1));
                        var virtualDir = new VirtualFileInfo(dirPath, isDirectory: true);
                        contents.Add(virtualDir);
                    }
                }
            }

            return new EnumerableDirectoryContents(contents);
        }

        public IFileInfo GetFileInfo(string subpath)
        {
            return new VirtualFileInfo(subpath);
        }

        public IChangeToken Watch(string filter)
        {
            return NoopChangeToken.Singleton;
        }
    }
}
