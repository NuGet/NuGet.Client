using Ionic.Zip;
using NuGet.Versioning;
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
                              <metadata{3}>
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
            <frameworkAssembly assemblyName='{0}' targetFramework='{1}' />
        </frameworkAssemblies>";

        private static string DependenciesStringFormat = @"<dependencies>
            <dependency id='{0}' version='{1}' />
        </dependencies>";

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

        public static FileInfo GetNet45TestPackage(string path, string packageId = "packageA", string packageVersion = "2.0.3")
        {
            ZipFile zipFile;
            FileInfo fileInfo = GetFileInfo(path, packageId, packageVersion, out zipFile);

            zipFile.AddEntry("tools/tool.exe", new byte[] { 0 });
            zipFile.AddEntry("lib/net45/test45.dll", new byte[] { 0 });

            SetSimpleNuspec(zipFile, packageId, packageVersion);
            zipFile.Save();

            return fileInfo;
        }

        public static FileInfo GetEmptyNet45TestPackage(string path, string packageId = "packageA", string packageVersion = "2.0.3")
        {
            ZipFile zipFile;
            FileInfo fileInfo = GetFileInfo(path, packageId, packageVersion, out zipFile);

            zipFile.AddEntry("lib/net45/", new byte[] { 0 });

            SetSimpleNuspec(zipFile, packageId, packageVersion);
            zipFile.Save();

            return fileInfo;
        }

        public static FileInfo GetLegacyContentPackage(string path, string packageId, string packageVersion)
        {
            ZipFile zipFile;
            FileInfo fileInfo = GetFileInfo(path, packageId, packageVersion, out zipFile);

            zipFile.AddEntry("Content/", new byte[] { 0 });
            zipFile.AddEntry("Content/Scripts/", new byte[] { 0 });
            zipFile.AddEntry("Content/Scripts/test1.js", new byte[] { 0 });
            zipFile.AddEntry("Content/Scripts/test2.js", new byte[] { 0 });
            zipFile.AddEntry("Content/Scripts/test3.js", new byte[] { 0 });

            SetSimpleNuspec(zipFile, packageId, packageVersion);
            zipFile.Save();

            return fileInfo;
        }

        public static FileInfo GetPackageWithPPFiles(string path, string packageId, string packageVersion)
        {
            ZipFile zipFile;
            FileInfo fileInfo = GetFileInfo(path, packageId, packageVersion, out zipFile);

            zipFile.AddEntry("Content/", new byte[] { 0 });
            zipFile.AddEntry("Content/Bar.cs.pp", new byte[] { 0 });
            zipFile.AddEntry("Content/Foo.cs.pp", new byte[] { 0 });

            SetSimpleNuspec(zipFile, packageId, packageVersion);
            zipFile.Save();

            return fileInfo;
        }

        public static FileInfo GetContentPackageWithTargetFramework(string path, string packageId, string packageVersion)
        {
            ZipFile zipFile;
            FileInfo fileInfo = GetFileInfo(path, packageId, packageVersion, out zipFile);

            zipFile.AddEntry("Content/", new byte[] { 0 });
            zipFile.AddEntry("Content/net45/", new byte[] { 0 });
            zipFile.AddEntry("Content/net45/Scripts/", new byte[] { 0 });
            zipFile.AddEntry("Content/net45/Scripts/net45test1.js", new byte[] { 0 });
            zipFile.AddEntry("Content/net45/Scripts/net45test2.js", new byte[] { 0 });
            zipFile.AddEntry("Content/net45/Scripts/net45test3.js", new byte[] { 0 });

            SetSimpleNuspec(zipFile, packageId, packageVersion);
            zipFile.Save();

            return fileInfo;
        }
        
        public static FileInfo GetPackageWithWebConfigTransform(string path, string packageId, string packageVersion, string webConfigTransformContent)
        {
            ZipFile zipFile;
            FileInfo fileInfo = GetFileInfo(path, packageId, packageVersion, out zipFile);

            zipFile.AddEntry("Content/", new byte[] { 0 });
            zipFile.AddEntry("Content/web.config.transform", webConfigTransformContent);
            SetSimpleNuspec(zipFile, packageId, packageVersion);
            zipFile.Save();

            return fileInfo;
        }

        public static FileInfo GetPackageWithBuildFiles(string path, string packageId, string packageVersion)
        {
            ZipFile zipFile;
            FileInfo fileInfo = GetFileInfo(path, packageId, packageVersion, out zipFile);

            zipFile.AddEntry("build/net45/" + packageId + ".targets", new byte[] { 0 });
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

        public static FileInfo GetPackageWithPowershellScripts(string path, string packageId, string packageVersion)
        {
            ZipFile zipFile;
            FileInfo fileInfo = GetFileInfo(path, packageId, packageVersion, out zipFile);

            zipFile.AddEntry("tools/init.ps1", new byte[] { 0 });
            zipFile.AddEntry("tools/net45/install.ps1", new byte[] { 0 });
            zipFile.AddEntry("tools/net45/uninstall.ps1", new byte[] { 0 });
            SetSimpleNuspec(zipFile, packageId, packageVersion);
            zipFile.Save();

            return fileInfo;
        }

        public static FileInfo GetLegacySolutionLevelPackage(string path, string packageId, string packageVersion)
        {
            ZipFile zipFile;
            FileInfo fileInfo = GetFileInfo(path, packageId, packageVersion, out zipFile);

            zipFile.AddEntry("tools/tool.exe", new byte[] { 0 });
            SetSimpleNuspec(zipFile, packageId, packageVersion);
            zipFile.Save();

            return fileInfo;
        }

        public static FileInfo GetInvalidPackage(string path, string packageId, string packageVersion)
        {
            ZipFile zipFile;
            FileInfo fileInfo = GetFileInfo(path, packageId, packageVersion, out zipFile);

            SetSimpleNuspec(zipFile, packageId, packageVersion);
            zipFile.Save();

            return fileInfo;
        }

        public static FileInfo GetEmptyPackageWithDependencies(string path, string packageId, string packageVersion)
        {
            ZipFile zipFile;
            FileInfo fileInfo = GetFileInfo(path, packageId, packageVersion, out zipFile);

            SetSimpleNuspec(zipFile, packageId, packageVersion, false, null, true);
            zipFile.Save();

            return fileInfo;
        }

        public static FileInfo GetPackageWithMinClientVersion(string path, string packageId, string packageVersion, SemanticVersion minClientVersion)
        {
            ZipFile zipFile;
            FileInfo fileInfo = GetFileInfo(path, packageId, packageVersion, out zipFile);

            SetSimpleNuspec(zipFile, packageId, packageVersion, false, minClientVersion);
            zipFile.Save();

            return fileInfo;
        }

        private static FileInfo GetFileInfo(string path, string packageId, string packageVersion, out ZipFile zipFile)
        {
            string file = Guid.NewGuid().ToString() + ".nupkg";
            FileInfo fileInfo = new FileInfo(file);

            zipFile = new ZipFile(fileInfo.FullName);

            return fileInfo;
        }

        public static void SetSimpleNuspec(ZipFile zipFile, string packageId, string packageVersion, bool frameworkAssemblies = false, SemanticVersion minClientVersion = null, bool dependencies = false)
        {
            zipFile.AddEntry(packageId + ".nuspec", GetSimpleNuspecString(packageId, packageVersion, frameworkAssemblies, minClientVersion, dependencies), Encoding.UTF8);
        }

        private static readonly string MinClientVersionStringFormat = "minClientVersion=\"{0}\"";
        private static string GetSimpleNuspecString(string packageId, string packageVersion, bool frameworkAssemblies, SemanticVersion minClientVersion, bool dependencies)
        {
            string frameworkAssemblyReferences = frameworkAssemblies ?
                String.Format(FrameworkAssembliesStringFormat, "System.Xml", "net45") : String.Empty;

            string minClientVersionString = minClientVersion == null ? String.Empty :
                String.Format(MinClientVersionStringFormat, minClientVersion.ToNormalizedString());

            string dependenciesString = dependencies ?
                String.Format(DependenciesStringFormat, "Owin", "1.0") : String.Empty;
            return String.Format(NuspecStringFormat, packageId, packageVersion,
                String.Join(Environment.NewLine, frameworkAssemblyReferences, dependenciesString),
                minClientVersionString);
        }
    }
}
