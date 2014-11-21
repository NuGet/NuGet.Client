using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace NuGet.Packaging
{
    /// <summary>
    /// Reads a NuGet v3.0.0 nupkg.
    /// </summary>
    public class PackageReader : IPackageReader
    {
        private readonly IFileSystem _fileSystem;

        public PackageReader(Stream packageStream)
            : this(packageStream, false)
        {

        }

        public PackageReader(Stream packageStream, bool leaveStreamOpen)
            : this(new ZipFileSystem(packageStream, leaveStreamOpen))
        {

        }

        public PackageReader(IFileSystem fileSystem)
        {
            if (fileSystem == null)
            {
                throw new ArgumentNullException("fileSystem");
            }

            _fileSystem = fileSystem;
        }

        public Stream GetPackedManifest()
        {
            string path = FileSystem.GetFiles().Where(f => f.Equals("/" + PackagingConstants.PackedManifestFileName, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

            return GetStream(path);
        }

        public PackageIdentity GetIdentity()
        {
            var nuspec = new NuspecReader(GetNuspec());

            return new PackageIdentity(nuspec.GetId(), NuGetVersion.Parse(nuspec.GetVersion()));
        }

        public Stream GetNuspec()
        {
            string path = FileSystem.GetFiles().Where(f => f.EndsWith(PackagingConstants.NuspecExtension, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

            return GetStream(path);
        }

        public IFileSystem FileSystem
        {
            get
            {
                return _fileSystem;
            }
        }

        public ComponentTree GetComponentTree()
        {
            XDocument doc = XDocument.Load(GetPackedManifest());

            return new ComponentTree(doc.Root);
        }

        private Stream GetStream(string path)
        {
            Stream stream = null;

            if (!String.IsNullOrEmpty(path))
            {
                stream = FileSystem.OpenFile(path);
            }

            return stream;
        }

        public void Dispose()
        {

        }
    }
}
