using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackagingCore
{
    /// <summary>
    /// A zipped nupkg.
    /// </summary>
    public class ZipFileSystem : IFileSystem, IDisposable
    {
        private readonly ZipArchive _zip;
        private IReadOnlyCollection<ZipArchiveEntry> _entries;

        public ZipFileSystem(Stream zipStream)
            : this(zipStream, false)
        {

        }

        public ZipFileSystem(Stream zipStream, bool leaveStreamOpen)
        {
            _zip = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveStreamOpen);
        }

        private IReadOnlyCollection<ZipArchiveEntry> Entries
        {
            get
            {
                if (_entries == null)
                {
                    _entries = _zip.Entries;
                }

                return _entries;
            }
        }

        public IEnumerable<string> GetFiles()
        {
            return Entries.Select(e => UnescapePath(e.FullName));
        }

        private static string UnescapePath(string path)
        {
            if (path != null && path.IndexOf('%') > -1)
            {
                return Uri.UnescapeDataString(path);
            }

            return path;
        }

        public Stream OpenFile(string path)
        {
            var entry = GetEntry(path);
            return entry.Open();
        }

        public IEnumerable<string> GetFolders(string path)
        {
            var entry = GetEntry(path);

            // TODO: stop returning the name
            return entry.FullName.Split('/');
        }

        private ZipArchiveEntry GetEntry(string path)
        {
            var entry = Entries.Where(e => e.FullName == path).FirstOrDefault();

            if (entry == null)
            {
                throw new FileNotFoundException(path);
            }

            return entry;
        }

        public void Dispose()
        {
            _zip.Dispose();
        }
    }
}
