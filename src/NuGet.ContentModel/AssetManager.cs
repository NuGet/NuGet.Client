using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace NuGet.ContentModel
{
    public static class AssetManager
    {
        public static IEnumerable<Asset> GetPackageAssets(string manifestPath)
        {
            var spec = XDocument.Load(manifestPath);

            var contentsElement = spec.Root.Element("contents");

            if (contentsElement == null)
            {
                return GetContentItemsFromDisk(Path.GetDirectoryName(manifestPath));
            }

            var contents = contentsElement.Elements()
                            .Select(e => new Asset
                            {
                                Path = e.Attribute("path").Value,
                                Link = e.Attribute("link")?.Value
                            });

            return contents;
        }

        private static IEnumerable<Asset> GetContentItemsFromDisk(string packageDirectory)
        {
            packageDirectory = EnsureTrailingSlash(packageDirectory);

            foreach (var path in Directory.EnumerateFiles(packageDirectory, "*.*", SearchOption.AllDirectories))
            {
                var item = new Asset();
                if (Path.GetExtension(path) == ".nuspec" ||
                    Path.GetExtension(path) == ".nupkg" ||
                    Path.GetExtension(path) == ".sha512")
                {
                    continue;
                }

                item.Path = path.Substring(packageDirectory.Length).Replace('\\', '/');
                yield return item;
            }
        }

        private static string EnsureTrailingSlash(string path)
        {
            if (path.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }
    }
}