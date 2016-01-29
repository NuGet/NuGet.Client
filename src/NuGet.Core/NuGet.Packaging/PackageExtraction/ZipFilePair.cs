using System.IO;
using System.IO.Compression;

namespace NuGet.Packaging
{
    public class ZipFilePair
    {
        private readonly ZipArchiveEntry _packageEntry;
        private readonly string _fileFullPath;

        public string FileFullPath => _fileFullPath;
        public ZipArchiveEntry PackageEntry => _packageEntry;

        public ZipFilePair(string fileFullPath, ZipArchiveEntry entry)
        {
            _fileFullPath = fileFullPath;
            _packageEntry = entry;
        }

        public bool IsInstalled()
        {
            return FileFullPath != null && PackageEntry != null && File.Exists(FileFullPath);
        }
    }
}