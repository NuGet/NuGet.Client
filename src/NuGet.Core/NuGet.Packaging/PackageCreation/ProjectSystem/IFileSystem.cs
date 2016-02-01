using System;
using System.Collections.Generic;
using System.IO;

namespace NuGet
{
    public interface IFileSystem
    {
        ILogger Logger { get; set; }
        string Root { get; }
        void DeleteDirectory(string path, bool recursive);
        IEnumerable<string> GetFiles(string path, string filter, bool recursive);
        IEnumerable<string> GetDirectories(string path);
        string GetFullPath(string path);
        void DeleteFile(string path);
        void DeleteFiles(IEnumerable<IPackageFile> files, string rootDir);

        bool FileExists(string path);
        bool DirectoryExists(string path);
        void AddFile(string path, Stream stream);
        void AddFile(string path, Action<Stream> writeToStream);
        void AddFiles(IEnumerable<IPackageFile> files, string rootDir);

        void MakeFileWritable(string path);
        void MoveFile(string source, string destination);
        Stream CreateFile(string path);
        Stream OpenFile(string path);
        DateTimeOffset GetLastModified(string path);
        DateTimeOffset GetCreated(string path);
        DateTimeOffset GetLastAccessed(string path);
    }
}