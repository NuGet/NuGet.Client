using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.Test.Utility
{
    public static class TargetsUtility
    {
        /// <summary>
        /// Read a targets or props file and find all package related items.
        /// </summary>
        public static Dictionary<PackageIdentity, List<XElement>> GetMSBuildPackageItems(string path)
        {
            if (File.Exists(path))
            {
                return GetMSBuildPackageItems(XDocument.Load(path));
            }

            return new Dictionary<PackageIdentity, List<XElement>>();
        }

        /// <summary>
        /// Read a targets or props file and find all package related items.
        /// </summary>
        public static Dictionary<PackageIdentity, List<XElement>> GetMSBuildPackageItems(XDocument doc)
        {
            var results = new Dictionary<PackageIdentity, List<XElement>>();

            foreach (var item in doc.Descendants(XName.Get("NuGetPackageId", "http://schemas.microsoft.com/developer/msbuild/2003")))
            {
                var versionString = item.Parent.Element(XName.Get("NuGetPackageVersion", "http://schemas.microsoft.com/developer/msbuild/2003")).Value;

                var identity = new PackageIdentity(item.Value, NuGetVersion.Parse(versionString));

                if (!results.ContainsKey(identity))
                {
                    results.Add(identity, new List<XElement>());
                }

                results[identity].Add(item.Parent);
            }

            return results;
        }

        /// <summary>
        /// Read a targets or props file imports and find all package related items.
        /// </summary>
        public static List<XElement> GetMSBuildPackageImports(string path)
        {
            if (File.Exists(path))
            {
                return GetMSBuildPackageImports(XDocument.Load(path));
            }

            return new List<XElement>();
        }

        /// <summary>
        /// Read a targets or props file imports and find all package related items.
        /// </summary>
        public static List<XElement> GetMSBuildPackageImports(XDocument doc)
        {
            return doc.Descendants(XName.Get("Import", "http://schemas.microsoft.com/developer/msbuild/2003")).ToList();
        }
    }
}
