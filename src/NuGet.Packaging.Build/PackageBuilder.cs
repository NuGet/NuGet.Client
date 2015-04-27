using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace NuGet.Packaging.Build
{
    public class PackageBuilder
    {
        private List<IPackageFile> _files = new List<IPackageFile>();

        public MetadataBuilder Manifest { get; } = new MetadataBuilder();

        public string RelativePathRoot { get; set; }

        public void AddFile(string sourcePath, string targetPath, int? version = null)
        {
            sourcePath = Path.GetFullPath(Path.Combine(RelativePathRoot, sourcePath));
            AddFile(new PhysicalPackageFile(sourcePath, targetPath, version));
        }

        public void AddFilePattern(string include, string exclude, string targetPath)
        {
            // AddFile(new PhysicalPackageFile(sourcePath, targetPath));
        }

        public void AddFilePattern(string include, string targetPath)
        {
            // AddFile(new PhysicalPackageFile(sourcePath, targetPath));
        }

        public void AddFile(IPackageFile file)
        {
            _files.Add(file);
        }

        public void Save(Stream stream)
        {
            using (var package = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                // Validate and write the manifest
                WriteManifest(package);

                // Write the files to the package
                var extensions = WriteFiles(package);

                extensions.Add("nuspec");

                WriteOpcContentTypes(package, extensions);
            }
        }

        private void WriteManifest(ZipArchive package)
        {
            string path = Manifest.GetMetadataValue("id") + ".nuspec";

            WriteOpcManifestRelationship(package, path);

            var entry = package.CreateEntry(path, CompressionLevel.Optimal);

            using (Stream stream = entry.Open())
            {
                new NuSpecFormatter().Save(Manifest, stream);
            }
        }

        private HashSet<string> WriteFiles(ZipArchive package)
        {
            var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in _files)
            {
                var entry = package.CreateEntry(file.Path, CompressionLevel.Optimal);
                using (var stream = entry.Open())
                {
                    file.GetStream().CopyTo(stream);
                }

                extensions.Add(Path.GetExtension(file.Path).Substring(1));
            }

            return extensions;
        }

        private void WriteOpcManifestRelationship(ZipArchive package, string path)
        {
            var relsEntry = package.CreateEntry("_rels/.rels", CompressionLevel.Optimal);

            using (var writer = new StreamWriter(relsEntry.Open()))
            {
                writer.Write(String.Format(@"<?xml version=""1.0"" encoding=""utf-8""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
    <Relationship Type=""http://schemas.microsoft.com/packaging/2010/07/manifest"" Target=""/{0}"" Id=""{1}"" />
</Relationships>", path, GenerateRelationshipId()));
                writer.Flush();
            }
        }

        private static void WriteOpcContentTypes(ZipArchive package, HashSet<string> extensions)
        {
            // OPC backwards compatibility
            var relsEntry = package.CreateEntry("[Content_Types].xml", CompressionLevel.Optimal);

            using (var writer = new StreamWriter(relsEntry.Open()))
            {
                writer.Write(@"<?xml version=""1.0"" encoding=""utf-8""?>
<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">
    <Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml"" />");
                foreach (var extension in extensions)
                {
                    writer.Write(@"<Default Extension=""" + extension + @""" ContentType=""application/octet"" />");
                }
                writer.Write("</Types>");
                writer.Flush();
            }
        }

        // Generate a relationship id for compatibility
        private string GenerateRelationshipId()
        {
            return "R" + Guid.NewGuid().ToString("N").Substring(0, 16);
        }
    }
}