// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Test.Utility
{
    public static class TestPackages
    {
        private static readonly string NuspecStringFormat = @"<?xml version=""1.0"" encoding=""utf-8""?>
                            <package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
                              <metadata>
                                <id>{0}</id>
                                <version>{1}</version>
                                <authors>Author1, author2</authors>
                                <description>Sample description</description>
                                <language>{2}</language>
                                <projectUrl>http://www.nuget.org/</projectUrl>
                                <licenseUrl>http://www.nuget.org/license</licenseUrl>
                                {3}
                              </metadata>
                            </package>";

        private static readonly string FrameworkAssembliesStringFormat = @"<frameworkAssemblies>
            <frameworkAssembly assemblyName='{0}' targetFramework='{1}' />
        </frameworkAssemblies>";

        private static readonly string DependenciesStringFormat = @"<dependencies>
            <dependency id='{0}' version='{1}' />
        </dependencies>";

        private static readonly byte[] ZeroContent = new byte[] { 0 };

        public static ZipArchive GetZip(FileInfo file)
        {
            return new ZipArchive(file.OpenRead());
        }

        public static ZipArchive GetZip(string file)
        {
            return new ZipArchive(File.OpenRead(file));
        }

        public static TestPackageInfo GetNearestReferenceFilteringPackage()
        {
            var file = new TempFile();
            var result = new TestPackageInfo(file)
            {
                Id = "RefPackage",
                Version = "1.0.0",
            };

            using (var zip = new ZipArchive(File.Create(result.File.FullName), ZipArchiveMode.Create))
            {
                zip.AddEntry("lib/net40/one.dll", ZeroContent);
                zip.AddEntry("lib/net40/three.dll", ZeroContent);
                zip.AddEntry("lib/net40/two.dll", ZeroContent);

                zip.AddEntry("lib/sl40/a.dll", ZeroContent);
                zip.AddEntry("lib/sl40/b.dll", ZeroContent);

                zip.AddEntry("packageA.nuspec", @"<?xml version=""1.0"" encoding=""utf-8""?>
                                      <package xmlns=""http://schemas.microsoft.com/packaging/2013/01/nuspec.xsd"">
                                         <metadata>
                                        <id>" + result.Id + @"RefPackage</id>
                                            <version>" + result.Version + @"</version>
                                            <title />
                                           <references>
                                               <group targetFramework=""net"">
                                                   <reference file=""one.dll"" />
                                                   <reference file=""three.dll"" />
                                               </group>
                                                <group targetFramework=""silverlight40"">
                                                    <reference file=""a.dll"" />
                                               </group>
                                        </references>
                                    </metadata>
                                    <files>
                                        <file src=""lib\net40\one.dll"" target=""lib\net40\one.dll"" />
                                        <file src=""lib\net40\three.dll"" target=""lib\net40\three.dll"" />
                                        <file src=""lib\net40\two.dll"" target=""lib\net40\two.dll"" />

                                        <file src=""lib\sl40\a.dll"" target=""lib\sl40\a.dll"" />
                                        <file src=""lib\sl40\b.dll"" target=""lib\sl40\b.dll"" />
                                   </files>
                                </package>", Encoding.UTF8);
            }

            return result;
        }


        public static TestPackageInfo GetPackageWithNupkgCopy()
        {
            var file = new TempFile();
            var result = new TestPackageInfo(file)
            {
                Id = "RefPackage",
                Version = "1.0.0",
            };

            using (var zip = new ZipArchive(File.Create(result.File.FullName), ZipArchiveMode.Create))
            {
                zip.AddEntry(result.Id + "." + result.Version + ".nupkg", ZeroContent);
                zip.AddEntry("lib/net40/one.dll", ZeroContent);
                zip.AddEntry("lib/net40/three.dll", ZeroContent);
                zip.AddEntry("lib/net40/two.dll", ZeroContent);

                zip.AddEntry("lib/sl40/a.dll", ZeroContent);
                zip.AddEntry("lib/sl40/b.dll", ZeroContent);

                zip.AddEntry("packageA.nuspec", @"<?xml version=""1.0"" encoding=""utf-8""?>
                                      <package xmlns=""http://schemas.microsoft.com/packaging/2013/01/nuspec.xsd"">
                                         <metadata>
                                        <id>" + result.Id + @"RefPackage</id>
                                            <version>" + result.Version + @"</version>
                                            <title />
                                           <references>
                                               <group targetFramework=""net"">
                                                   <reference file=""one.dll"" />
                                                   <reference file=""three.dll"" />
                                               </group>
                                                <group targetFramework=""silverlight40"">
                                                    <reference file=""a.dll"" />
                                               </group>
                                        </references>
                                    </metadata>
                                    <files>
                                        <file src=""lib\net40\one.dll"" target=""lib\net40\one.dll"" />
                                        <file src=""lib\net40\three.dll"" target=""lib\net40\three.dll"" />
                                        <file src=""lib\net40\two.dll"" target=""lib\net40\two.dll"" />

                                        <file src=""lib\sl40\a.dll"" target=""lib\sl40\a.dll"" />
                                        <file src=""lib\sl40\b.dll"" target=""lib\sl40\b.dll"" />
                                   </files>
                                </package>", Encoding.UTF8);
            }

            return result;
        }

        public static TempFile GetLegacyFolderPackage()
        {
            var file = new TempFile();

            using (var zip = new ZipArchive(File.Create(file), ZipArchiveMode.Create))
            {
                zip.AddEntry("lib/a.dll", ZeroContent);
                zip.AddEntry("lib/35/b.dll", ZeroContent);
                zip.AddEntry("lib/40/test40.dll", ZeroContent);
                zip.AddEntry("lib/40/x86/testx86.dll", ZeroContent);
                zip.AddEntry("lib/45/a.dll", ZeroContent);

                zip.AddEntry("packageA.nuspec", @"<?xml version=""1.0"" encoding=""utf-8""?>
                            <package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
                              <metadata>
                                <id>packageA</id>
                                <version>2.0.3</version>
                                <authors>Author1, author2</authors>
                                <description>Sample description</description>
                                <language>en-US</language>
                                <projectUrl>http://www.nuget.org/</projectUrl>
                                <licenseUrl>http://www.nuget.org/license</licenseUrl>
                              </metadata>
                            </package>", Encoding.UTF8);
            }

            return file;
        }

        public static TempFile GetLegacyTestPackage()
        {
            var file = new TempFile();

            using (var zip = new ZipArchive(File.Create(file), ZipArchiveMode.Create))
            {
                zip.AddEntry("lib/test.dll", ZeroContent);
                zip.AddEntry("lib/net40/test40.dll", ZeroContent);
                zip.AddEntry("lib/net40/test40b.dll", ZeroContent);
                zip.AddEntry("lib/net45/test45.dll", ZeroContent);

                zip.AddEntry("packageA.nuspec", @"<?xml version=""1.0"" encoding=""utf-8""?>
                            <package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
                              <metadata>
                                <id>packageA</id>
                                <version>2.0.3</version>
                                <authors>Author1, author2</authors>
                                <description>Sample description</description>
                                <language>en-US</language>
                                <projectUrl>http://www.nuget.org/</projectUrl>
                                <licenseUrl>http://www.nuget.org/license</licenseUrl>
                                <dependencies>
                                   <group>
                                      <dependency id=""RouteMagic"" version=""1.1.0"" />
                                   </group>
                                   <group targetFramework=""net40"">
                                      <dependency id=""jQuery"" />
                                      <dependency id=""WebActivator"" />
                                   </group>
                                   <group targetFramework=""sl30"">
                                   </group>
                                </dependencies>
                              </metadata>
                            </package>", Encoding.UTF8);
            }

            return file;
        }

        public static TempFile GetPackageWithPackageTypes()
        {
            var file = new TempFile();

            using (var zip = new ZipArchive(File.Create(file), ZipArchiveMode.Create))
            {
                zip.AddEntry("lib/net40/test40.dll", ZeroContent);

                zip.AddEntry("packageA.nuspec", @"<?xml version=""1.0"" encoding=""utf-8""?>
                            <package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
                              <metadata>
                                <id>packageA</id>
                                <version>2.0.3</version>
                                <authors>Author1, author2</authors>
                                <description>Sample description</description>
                                <packageTypes>
                                  <packageType name=""foo"" />
                                  <packageType name=""bar"" version=""2.0.0"" />
                                </packageTypes>
                              </metadata>
                            </package>", Encoding.UTF8);
            }

            return file;
        }

        public static TempFile GetLegacyTestPackageMinClient(string minClientVersion)
        {
            var file = new TempFile();

            using (var zip = new ZipArchive(File.Create(file), ZipArchiveMode.Create))
            {
                zip.AddEntry("lib/test.dll", ZeroContent);
                zip.AddEntry("lib/net40/test40.dll", ZeroContent);
                zip.AddEntry("lib/net40/test40b.dll", ZeroContent);
                zip.AddEntry("lib/net45/test45.dll", ZeroContent);

                var nuspec = string.Format(
                    CultureInfo.InvariantCulture,
                    @"<?xml version=""1.0"" encoding=""utf-8""?>
                            <package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
                              <metadata minClientVersion=""{0}"">
                                <id>packageA</id>
                                <version>2.0.3</version>
                                <authors>Author1, author2</authors>
                                <description>Sample description</description>
                                <language>en-US</language>
                                <projectUrl>http://www.nuget.org/</projectUrl>
                                <licenseUrl>http://www.nuget.org/license</licenseUrl>
                                <dependencies>
                                   <group>
                                      <dependency id=""RouteMagic"" version=""1.1.0"" />
                                   </group>
                                   <group targetFramework=""net40"">
                                      <dependency id=""jQuery"" />
                                      <dependency id=""WebActivator"" />
                                   </group>
                                   <group targetFramework=""sl30"">
                                   </group>
                                </dependencies>
                              </metadata>
                            </package>",
                    minClientVersion);

                zip.AddEntry("packageA.nuspec", nuspec, Encoding.UTF8);

                return file;
            }
        }

        public static TempFile GetLibSubFolderPackage()
        {
            var file = new TempFile();

            using (var zip = new ZipArchive(File.Create(file), ZipArchiveMode.Create))
            {
                zip.AddEntry("lib/net40/test40.dll", ZeroContent);
                zip.AddEntry("lib/net40/x86/testx86.dll", ZeroContent);

                zip.AddEntry("packageA.nuspec", @"<?xml version=""1.0"" encoding=""utf-8""?>
                            <package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
                              <metadata>
                                <id>packageA</id>
                                <version>2.0.3</version>
                                <authors>Author1, author2</authors>
                                <description>Sample description</description>
                                <language>en-US</language>
                                <projectUrl>http://www.nuget.org/</projectUrl>
                                <licenseUrl>http://www.nuget.org/license</licenseUrl>
                                <dependencies>
                                   <group>
                                      <dependency id=""RouteMagic"" version=""1.1.0"" />
                                   </group>
                                   <group targetFramework=""net40"">
                                      <dependency id=""jQuery"" />
                                      <dependency id=""WebActivator"" />
                                   </group>
                                   <group targetFramework=""sl30"">
                                   </group>
                                </dependencies>
                              </metadata>
                            </package>", Encoding.UTF8);
            }

            return file;
        }

        public static TempFile GetLibEmptyFolderPackage()
        {
            var file = new TempFile();

            using (var zip = new ZipArchive(File.Create(file), ZipArchiveMode.Create))
            {
                zip.AddEntry("lib/net40/test40.dll", ZeroContent);
                zip.AddEntry("lib/net40/x86/testx86.dll", ZeroContent);
                zip.AddEntry("lib/a.dll", ZeroContent);
                zip.AddEntry("lib/x86/b.dll", ZeroContent);
                zip.AddEntry("lib/net45/_._", ZeroContent);

                zip.AddEntry("packageA.nuspec", @"<?xml version=""1.0"" encoding=""utf-8""?>
                            <package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
                              <metadata>
                                <id>packageA</id>
                                <version>2.0.3</version>
                                <authors>Author1, author2</authors>
                                <description>Sample description</description>
                                <language>en-US</language>
                                <projectUrl>http://www.nuget.org/</projectUrl>
                                <licenseUrl>http://www.nuget.org/license</licenseUrl>
                                <dependencies>
                                   <group>
                                      <dependency id=""RouteMagic"" version=""1.1.0"" />
                                   </group>
                                   <group targetFramework=""net40"">
                                      <dependency id=""jQuery"" />
                                      <dependency id=""WebActivator"" />
                                   </group>
                                   <group targetFramework=""sl30"">
                                   </group>
                                </dependencies>
                              </metadata>
                            </package>", Encoding.UTF8);
            }

            return file;
        }

        public static TempFile GetLegacyTestPackageWithReferenceGroups()
        {
            var file = new TempFile();

            using (var zip = new ZipArchive(File.Create(file), ZipArchiveMode.Create))
            {
                zip.AddEntry("lib/test.dll", ZeroContent);
                zip.AddEntry("lib/net40/test40.dll", ZeroContent);
                zip.AddEntry("lib/net40/test40b.dll", ZeroContent);
                zip.AddEntry("lib/net45/test45.dll", ZeroContent);

                zip.AddEntry("packageA.nuspec", @"<?xml version=""1.0"" encoding=""utf-8""?>
                            <package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
                              <metadata>
                                <id>packageA</id>
                                <version>2.0.3</version>
                                <authors>Author1, author2</authors>
                                <description>Sample description</description>
                                <language>en-US</language>
                                <projectUrl>http://www.nuget.org/</projectUrl>
                                <licenseUrl>http://www.nuget.org/license</licenseUrl>
                                <dependencies>
                                   <group>
                                      <dependency id=""RouteMagic"" version=""1.1.0"" />
                                   </group>
                                   <group targetFramework=""net40"">
                                      <dependency id=""jQuery"" />
                                      <dependency id=""WebActivator"" />
                                   </group>
                                   <group targetFramework=""sl30"">
                                   </group>
                                </dependencies>
                                <references>
                                  <group targetFramework=""net45"">
                                      <reference file=""test45.dll"" />
                                  </group>
                                  <group>
                                    <reference file=""test.dll"" />
                                  </group>
                                </references>
                                <frameworkAssemblies>
                                  <frameworkAssembly assemblyName=""wp80Assem"" targetFramework=""wp80"" />
                                  <frameworkAssembly assemblyName=""anyAssem""  />
                                </frameworkAssemblies>
                              </metadata>
                            </package>", Encoding.UTF8);
            }

            return file;
        }

        public static TempFile GetLegacyTestPackageWithPre25References()
        {
            var file = new TempFile();

            using (var zip = new ZipArchive(File.Create(file), ZipArchiveMode.Create))
            {
                zip.AddEntry("lib/test.dll", ZeroContent);
                zip.AddEntry("lib/testa.dll", ZeroContent);
                zip.AddEntry("lib/net40/test.dll", ZeroContent);
                zip.AddEntry("lib/net40/testb.dll", ZeroContent);
                zip.AddEntry("lib/net45/test45.dll", ZeroContent);
                zip.AddEntry("lib/net451/test.dll", ZeroContent);

                zip.AddEntry("packageA.nuspec", @"<?xml version=""1.0"" encoding=""utf-8""?>
                            <package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
                              <metadata>
                                <id>packageA</id>
                                <version>2.0.3</version>
                                <authors>Author1, author2</authors>
                                <description>Sample description</description>
                                <language>en-US</language>
                                <projectUrl>http://www.nuget.org/</projectUrl>
                                <licenseUrl>http://www.nuget.org/license</licenseUrl>
                                <dependencies>
                                   <group>
                                      <dependency id=""RouteMagic"" version=""1.1.0"" />
                                   </group>
                                   <group targetFramework=""net40"">
                                      <dependency id=""jQuery"" />
                                      <dependency id=""WebActivator"" />
                                   </group>
                                   <group targetFramework=""sl30"">
                                   </group>
                                </dependencies>
                                <references>
                                  <reference file=""test.dll"" />
                                </references>
                                <frameworkAssemblies>
                                  <frameworkAssembly assemblyName=""wp80Assem"" targetFramework=""wp80"" />
                                  <frameworkAssembly assemblyName=""anyAssem""  />
                                </frameworkAssemblies>
                              </metadata>
                            </package>", Encoding.UTF8);
            }

            return file;
        }

        public static TempFile GetLegacyTestPackageWithInvalidPortableFrameworkFolderName()
        {
            var file = new TempFile();

            using (var zip = new ZipArchive(File.Create(file), ZipArchiveMode.Create))
            {
                zip.AddEntry("lib/test.dll", ZeroContent);
                zip.AddEntry("lib/net45/test45.dll", ZeroContent);
                zip.AddEntry("lib/portable-net+win+wpa+wp+sl+net-cf+netmf+MonoAndroid+MonoTouch+Xamarin.iOS/test.dll", ZeroContent);

                zip.AddEntry("packageA.nuspec", @"<?xml version=""1.0"" encoding=""utf-8""?>
                            <package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
                              <metadata>
                                <id>packageA</id>
                                <version>2.0.3</version>
                                <authors>Author1, author2</authors>
                                <description>Sample description</description>
                                <language>en-US</language>
                                <projectUrl>http://www.nuget.org/</projectUrl>
                                <licenseUrl>http://www.nuget.org/license</licenseUrl>
                              </metadata>
                            </package>", Encoding.UTF8);
            }

            return file;
        }

        public static TempFile GetLegacyContentPackage()
        {
            var file = new TempFile();

            using (var zip = new ZipArchive(File.Create(file), ZipArchiveMode.Create))
            {
                zip.AddEntry("content/Scripts/test.js", ZeroContent);
                zip.AddEntry("content/Scripts/test2.js", ZeroContent);
                zip.AddEntry("content/Scripts/test3.js", ZeroContent);

                zip.AddEntry("packageA.nuspec", @"<?xml version=""1.0"" encoding=""utf-8""?>
                            <package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
                              <metadata>
                                <id>contentPackage</id>
                                <version>2.0.3</version>
                                <authors>Author1, author2</authors>
                                <description>Sample description</description>
                                <language>en-US</language>
                                <projectUrl>http://www.nuget.org/</projectUrl>
                                <licenseUrl>http://www.nuget.org/license</licenseUrl>
                              </metadata>
                            </package>", Encoding.UTF8);
            }

            return file;
        }

        public static TempFile GetLegacyContentPackageWithFrameworks()
        {
            var file = new TempFile();

            using (var zip = new ZipArchive(File.Create(file), ZipArchiveMode.Create))
            {
                zip.AddEntry("content/net40/Scripts/test.js", ZeroContent);
                zip.AddEntry("content/net45/Scripts/test2.js", ZeroContent);
                zip.AddEntry("content/net451/Scripts/test3.js", ZeroContent);

                zip.AddEntry("packageA.nuspec", @"<?xml version=""1.0"" encoding=""utf-8""?>
                            <package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
                              <metadata>
                                <id>contentPackage</id>
                                <version>2.0.3</version>
                                <authors>Author1, author2</authors>
                                <description>Sample description</description>
                                <language>en-US</language>
                                <projectUrl>http://www.nuget.org/</projectUrl>
                                <licenseUrl>http://www.nuget.org/license</licenseUrl>
                              </metadata>
                            </package>", Encoding.UTF8);
            }

            return file;
        }

        public static TempFile GetLegacyContentPackageMixed()
        {
            var file = new TempFile();

            using (var zip = new ZipArchive(File.Create(file), ZipArchiveMode.Create))
            {
                zip.AddEntry("content/Scripts/test.js", ZeroContent);
                zip.AddEntry("content/net40/Scripts/test2.js", ZeroContent);
                zip.AddEntry("content/net40/Scripts/testb.js", ZeroContent);
                zip.AddEntry("content/net45/Scripts/test3.js", ZeroContent);

                zip.AddEntry("packageA.nuspec", @"<?xml version=""1.0"" encoding=""utf-8""?>
                            <package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
                              <metadata>
                                <id>contentPackage</id>
                                <version>2.0.3</version>
                                <authors>Author1, author2</authors>
                                <description>Sample description</description>
                                <language>en-US</language>
                                <projectUrl>http://www.nuget.org/</projectUrl>
                                <licenseUrl>http://www.nuget.org/license</licenseUrl>
                              </metadata>
                            </package>", Encoding.UTF8);
            }

            return file;
        }

        public static Stream GetTestPackageWithContentXmlFile()
        {
            var stream = new MemoryStream();

            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                zip.AddEntry("lib/a.dll", ZeroContent);
                zip.AddEntry("[Content_Types].xml", ZeroContent);
                zip.AddEntry("content/[Content_Types].xml", ZeroContent);

                zip.AddEntry("packageA.nuspec", @"<?xml version=""1.0"" encoding=""utf-8""?>
                            <package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
                              <metadata>
                                <id>packageA</id>
                                <version>2.0.3</version>
                                <authors>Author1, author2</authors>
                                <description>Sample description</description>
                                <language>en-US</language>
                                <projectUrl>http://www.nuget.org/</projectUrl>
                                <licenseUrl>http://www.nuget.org/license</licenseUrl>
                              </metadata>
                            </package>", Encoding.UTF8);
            }

            return stream;
        }

        public static async Task<FileInfo> GetPackageWithSHA512AtRoot(string path, string packageId, string packageVersion)
        {
            return await GeneratePackageAsync(path, packageId, packageVersion, DateTimeOffset.UtcNow.LocalDateTime,
                "lib/net45/A.dll",
                "lib/net45/B.sha512",
                $"{packageId}.${packageVersion}.nupkg.sha512",
                "C.sha512");
        }

        public static async Task<FileInfo> GetPackageWithNupkgAtRoot(string path, string packageId, string packageVersion)
        {
            return await GeneratePackageAsync(path, packageId, packageVersion, DateTimeOffset.UtcNow.LocalDateTime,
                "lib/net45/A.dll",
                "lib/net45/B.nupkg",
                $"{packageId}.{packageVersion}.nupkg");
        }

        public static async Task<FileInfo> GetRuntimePackageAsync(
            string path,
            string packageId,
            string packageVersion)
        {
           return await GeneratePackageAsync(
               path,
               packageId,
               packageVersion,
               DateTimeOffset.UtcNow.LocalDateTime,
               "lib/net45/A.dll");
        }

        public static async Task<FileInfo> GetSatellitePackageAsync(
            string path,
            string runtimePackageId,
            string packageVersion,
            string language)
        {
            return await GeneratePackageAsync(
                path,
                runtimePackageId + "." + language,
                packageVersion,
                language,
                DateTimeOffset.UtcNow.LocalDateTime,
                Path.Combine("lib/net45", language, runtimePackageId + ".resources.dll"));
        }

        public static async Task<FileInfo> GeneratePackageAsync(
            string path,
            string packageId,
            string packageVersion,
            DateTime entryModifiedTime,
            params string[] zipEntries)
        {
            return await GeneratePackageAsync(
                path,
                packageId,
                packageVersion,
                "en-US",
                entryModifiedTime,
                zipEntries);
        }

        public static async Task<FileInfo> GeneratePackageAsync(
            string path,
            string packageId,
            string packageVersion,
            string language,
            DateTime entryModifiedTime,
            params string[] zipEntries)
        {
            return await GeneratePackageAsync(
                path,
                packageId,
                packageVersion,
                language,
                entryModifiedTime,
                zipEntries,
                zipContents: Enumerable.Repeat(string.Empty, zipEntries.Count()));
        }

        public static async Task<FileInfo> GeneratePackageAsync(
            string path,
            string packageId,
            string packageVersion,
            string language,
            DateTime entryModifiedTime,
            IEnumerable<string> zipEntries,
            IEnumerable<string> zipContents,
            bool frameworkAssemblies = false,
            bool dependencies = false)
        {
            if (zipEntries == null)
            {
                throw new ArgumentNullException(nameof(zipEntries));
            }
            if (zipContents == null)
            {
                throw new ArgumentNullException(nameof(zipContents));
            }
            if (zipEntries.Count() != zipContents.Count())
            {
                throw new ArgumentException("TEST Exception: zipEntries.Length should be equal to zipContents.Length");
            }

            var fileInfo = GetFileInfo(path, packageId, packageVersion);

            using (var zip = new ZipArchive(File.Create(fileInfo.FullName), ZipArchiveMode.Update))
            {
                var tasks
                    = zipEntries.Zip(zipContents, async (entry, content) => await zip.AddEntryAsync(entry, content));
                var entries = await Task.WhenAll(tasks);
                entries.ToList().ForEach(entry => entry.LastWriteTime = entryModifiedTime);
                await SetSimpleNuspecAsync(
                    zip,
                    packageId,
                    packageVersion,
                    language,
                    frameworkAssemblies,
                    dependencies);
            }

            return fileInfo;
        }

        private static FileInfo GetFileInfo(string path, string packageId, string packageVersion)
        {
            var file = Path.Combine(path, $"{packageId}.{packageVersion}.nupkg");
            var fileInfo = new FileInfo(file);

            return fileInfo;
        }

        public static async Task<ZipArchiveEntry> SetSimpleNuspecAsync(ZipArchive zip,
            string packageId,
            string packageVersion,
            string language,
            bool frameworkAssemblies,
            bool dependencies)
        {
            return await zip.AddEntryAsync(
                $"{packageId}.nuspec",
                GetSimpleNuspecString(packageId, packageVersion, language, frameworkAssemblies, dependencies));
        }

        private static string GetSimpleNuspecString(string packageId,
            string packageVersion,
            string language,
            bool frameworkAssemblies,
            bool dependencies)
        {
            var frameworkAssemblyReferences = frameworkAssemblies ?
                string.Format(FrameworkAssembliesStringFormat, "System.Xml", "net45") : string.Empty;

            string dependenciesString;
            if (language == "en-US")
            {
                dependenciesString = dependencies ?
                    string.Format(DependenciesStringFormat, "Owin", "1.0") : string.Empty;
            }
            else
            {
                // Since language is not english, it should be a satellite package
                // Set the runtime package as a dependency
                dependenciesString = string.Format(DependenciesStringFormat,
                    packageId.Substring(0, packageId.Length - 1 - language.Length),
                    "[" + packageVersion + "]");
            }

            return string.Format(NuspecStringFormat, packageId, packageVersion, language,
                string.Join(Environment.NewLine, frameworkAssemblyReferences, dependenciesString));
        }

        public class TempFile : IDisposable
        {
            private readonly string _filePath;

            public TempFile()
            {
                string packagesFolder = Path.Combine(TestFileSystemUtility.NuGetTestFolder, "NuGetTestPackages");

                Directory.CreateDirectory(packagesFolder);

                int count = 0;
                do
                {
                    _filePath = Path.Combine(packagesFolder, Path.GetRandomFileName() + ".nupkg");
                    count++;
                }
                while (File.Exists(_filePath) && count < 3);

                if (count == 3)
                {
                    throw new InvalidOperationException("Failed to create a random file.");
                }
            }

            public static implicit operator string(TempFile f)
            {
                return f._filePath;
            }

            public void Dispose()
            {
                if (_filePath != null)
                {
                    try
                    {
                        File.Delete(_filePath);
                    }
                    catch
                    {
                    }
                }
            }
        }
    }
}
