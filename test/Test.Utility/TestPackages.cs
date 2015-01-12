using Ionic.Zip;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test.Utility
{
    public static class TestPackages
    {
        private static string NuspecStringFormat = @"<?xml version=""1.0"" encoding=""utf-8""?>
                            <package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
                              <metadata>
                                <id>{0}</id>
                                <version>{1}</version>
                                <authors>Author1, author2</authors>
                                <description>Sample description</description>
                                <language>en-US</language>
                                <projectUrl>http://www.nuget.org/</projectUrl>
                                <licenseUrl>http://www.nuget.org/license</licenseUrl>
                                {2}
                              </metadata>
                            </package>";

        private static string FrameworkAssembliesStringFormat = @"<frameworkAssemblies>
            <frameworkAssembly assemblyName='{0}' targetFramework='{1} />
        </frameworkAssemblies>";

        public static FileInfo GetLegacyTestPackage(string path, string packageId = "packageA", string packageVersion = "2.0.3")
        {
            ZipFile zipFile;
            FileInfo fileInfo = GetFileInfo(path, packageId, packageVersion, out zipFile);

            zipFile.AddEntry("lib/test.dll", new byte[] { 0 });
            zipFile.AddEntry("lib/net40/test40.dll", new byte[] { 0 });
            zipFile.AddEntry("lib/net40/test40b.dll", new byte[] { 0 });
            zipFile.AddEntry("lib/net45/test45.dll", new byte[] { 0 });

            SetSimpleNuspec(zipFile, packageId, packageVersion);
            zipFile.Save();

            return fileInfo;
        }

        public static FileInfo GetLegacyContentPackage(string path, string packageId, string packageVersion)
        {
            ZipFile zipFile;
            FileInfo fileInfo = GetFileInfo(path, packageId, packageVersion, out zipFile);

            zipFile.AddEntry("content/Scripts/test.js", new byte[] { 0 });
            zipFile.AddEntry("content/Scripts/test2.js", new byte[] { 0 });
            zipFile.AddEntry("content/Scripts/test3.js", new byte[] { 0 });

            SetSimpleNuspec(zipFile, packageId, packageVersion);
            zipFile.Save();

            return fileInfo;
        }

        public static FileInfo GetPackageWithFrameworkReference(string path, string packageId = "packageA", string packageVersion = "2.0.3")
        {
            ZipFile zipFile;
            FileInfo fileInfo = GetFileInfo(path, packageId, packageVersion, out zipFile);

            SetSimpleNuspec(zipFile, packageId, packageVersion, frameworkAssemblies: true);
            zipFile.Save();

            return fileInfo;
        }

        public static FileInfo GetFileInfo(string path, string packageId, string packageVersion, out ZipFile zipFile)
        {
            string file = Guid.NewGuid().ToString() + ".nupkg";
            FileInfo fileInfo = new FileInfo(file);

            zipFile = new ZipFile(fileInfo.FullName);

            return fileInfo;
        }

        public static void SetSimpleNuspec(ZipFile zipFile, string packageId, string packageVersion, bool frameworkAssemblies = false)
        {
            zipFile.AddEntry(packageId + ".nuspec", GetSimpleNuspecString(packageId, packageVersion, frameworkAssemblies), Encoding.UTF8);
        }

        private static string GetSimpleNuspecString(string packageId, string packageVersion, bool frameworkAssemblies)
        {
            string frameworkAssemblyReferences = frameworkAssemblies ?
                String.Format(FrameworkAssembliesStringFormat, "System.Xml", "net45") : String.Empty;
            return String.Format(NuspecStringFormat, packageId, packageVersion, frameworkAssemblyReferences);
        }
    }
}
