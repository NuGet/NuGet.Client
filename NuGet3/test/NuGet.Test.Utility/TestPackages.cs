// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace NuGet.Test.Utility
{
    public static class TestPackages
    {
        public class TestPackageInfo
        {
            public string Id { get; set; }
            public string Version { get; set; }
            public FileInfo File { get; set; }
        }

        public static ZipArchive GetZip(FileInfo file)
        {
            return new ZipArchive(file.OpenRead());
        }

        public static TestPackageInfo GetNearestReferenceFilteringPackage()
        {
            var file = Path.GetTempFileName() + ".nupkg";
            var result = new TestPackageInfo()
            {
                Id = "RefPackage",
                Version = "1.0.0",
                File = new FileInfo(file),
            };

            using (var zip = new ZipArchive(File.Create(result.File.FullName), ZipArchiveMode.Create))
            {
                zip.AddEntry("lib/net40/one.dll", new byte[] { 0 });
                zip.AddEntry("lib/net40/three.dll", new byte[] { 0 });
                zip.AddEntry("lib/net40/two.dll", new byte[] { 0 });

                zip.AddEntry("lib/sl40/a.dll", new byte[] { 0 });
                zip.AddEntry("lib/sl40/b.dll", new byte[] { 0 });

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
            var file = Path.GetTempFileName() + ".nupkg";
            var result = new TestPackageInfo()
            {
                Id = "RefPackage",
                Version = "1.0.0",
                File = new FileInfo(file),
            };

            using (var zip = new ZipArchive(File.Create(result.File.FullName), ZipArchiveMode.Create))
            {
                zip.AddEntry(result.Id + "." + result.Version + ".nupkg", new byte[] { 0 });
                zip.AddEntry("lib/net40/one.dll", new byte[] { 0 });
                zip.AddEntry("lib/net40/three.dll", new byte[] { 0 });
                zip.AddEntry("lib/net40/two.dll", new byte[] { 0 });

                zip.AddEntry("lib/sl40/a.dll", new byte[] { 0 });
                zip.AddEntry("lib/sl40/b.dll", new byte[] { 0 });

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

        public static FileInfo GetLegacyFolderPackage()
        {
            var file = Path.GetTempFileName() + ".nupkg";
            var result = new FileInfo(file);

            using (var zip = new ZipArchive(File.Create(result.FullName), ZipArchiveMode.Create))
            {
                zip.AddEntry("lib/a.dll", new byte[] { 0 });
                zip.AddEntry("lib/35/b.dll", new byte[] { 0 });
                zip.AddEntry("lib/40/test40.dll", new byte[] { 0 });
                zip.AddEntry("lib/40/x86/testx86.dll", new byte[] { 0 });
                zip.AddEntry("lib/45/a.dll", new byte[] { 0 });

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

            return result;
        }

        public static FileInfo GetLegacyTestPackage()
        {
            var file = Path.GetTempFileName() + ".nupkg";
            var result = new FileInfo(file);

            using (var zip = new ZipArchive(File.Create(result.FullName), ZipArchiveMode.Create))
            {
                zip.AddEntry("lib/test.dll", new byte[] { 0 });
                zip.AddEntry("lib/net40/test40.dll", new byte[] { 0 });
                zip.AddEntry("lib/net40/test40b.dll", new byte[] { 0 });
                zip.AddEntry("lib/net45/test45.dll", new byte[] { 0 });

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

            return result;
        }

        public static FileInfo GetLegacyTestPackageMinClient(string minClientVersion)
        {
            var file = Path.GetTempFileName() + ".nupkg";
            var result = new FileInfo(file);

            using (var zip = new ZipArchive(File.Create(result.FullName), ZipArchiveMode.Create))
            {
                zip.AddEntry("lib/test.dll", new byte[] { 0 });
                zip.AddEntry("lib/net40/test40.dll", new byte[] { 0 });
                zip.AddEntry("lib/net40/test40b.dll", new byte[] { 0 });
                zip.AddEntry("lib/net45/test45.dll", new byte[] { 0 });

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

                return result;
            }
        }

        public static FileInfo GetLibSubFolderPackage()
        {
            var file = Path.GetTempFileName() + ".nupkg";
            var result = new FileInfo(file);

            using (var zip = new ZipArchive(File.Create(result.FullName), ZipArchiveMode.Create))
            {
                zip.AddEntry("lib/net40/test40.dll", new byte[] { 0 });
                zip.AddEntry("lib/net40/x86/testx86.dll", new byte[] { 0 });

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

            return result;
        }

        public static FileInfo GetLibEmptyFolderPackage()
        {
            var file = Path.GetTempFileName() + ".nupkg";
            var result = new FileInfo(file);

            using (var zip = new ZipArchive(File.Create(result.FullName), ZipArchiveMode.Create))
            {
                zip.AddEntry("lib/net40/test40.dll", new byte[] { 0 });
                zip.AddEntry("lib/net40/x86/testx86.dll", new byte[] { 0 });
                zip.AddEntry("lib/a.dll", new byte[] { 0 });
                zip.AddEntry("lib/x86/b.dll", new byte[] { 0 });
                zip.AddEntry("lib/net45/_._", new byte[] { 0 });

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

            return result;
        }

        public static FileInfo GetLegacyTestPackageWithReferenceGroups()
        {
            var file = Path.GetTempFileName() + ".nupkg";
            var result = new FileInfo(file);

            using (var zip = new ZipArchive(File.Create(result.FullName), ZipArchiveMode.Create))
            {
                zip.AddEntry("lib/test.dll", new byte[] { 0 });
                zip.AddEntry("lib/net40/test40.dll", new byte[] { 0 });
                zip.AddEntry("lib/net40/test40b.dll", new byte[] { 0 });
                zip.AddEntry("lib/net45/test45.dll", new byte[] { 0 });

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

            return result;
        }

        public static FileInfo GetLegacyTestPackageWithPre25References()
        {
            var file = Path.GetTempFileName() + ".nupkg";
            var result = new FileInfo(file);

            using (var zip = new ZipArchive(File.Create(result.FullName), ZipArchiveMode.Create))
            {
                zip.AddEntry("lib/test.dll", new byte[] { 0 });
                zip.AddEntry("lib/testa.dll", new byte[] { 0 });
                zip.AddEntry("lib/net40/test.dll", new byte[] { 0 });
                zip.AddEntry("lib/net40/testb.dll", new byte[] { 0 });
                zip.AddEntry("lib/net45/test45.dll", new byte[] { 0 });
                zip.AddEntry("lib/net451/test.dll", new byte[] { 0 });

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

            return result;
        }

        public static FileInfo GetLegacyContentPackage()
        {
            var file = Path.GetTempFileName() + ".nupkg";
            var result = new FileInfo(file);

            using (var zip = new ZipArchive(File.Create(result.FullName), ZipArchiveMode.Create))
            {
                zip.AddEntry("content/Scripts/test.js", new byte[] { 0 });
                zip.AddEntry("content/Scripts/test2.js", new byte[] { 0 });
                zip.AddEntry("content/Scripts/test3.js", new byte[] { 0 });

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

            return result;
        }

        public static FileInfo GetLegacyContentPackageWithFrameworks()
        {
            var file = Path.GetTempFileName() + ".nupkg";
            var result = new FileInfo(file);

            using (var zip = new ZipArchive(File.Create(result.FullName), ZipArchiveMode.Create))
            {
                zip.AddEntry("content/net40/Scripts/test.js", new byte[] { 0 });
                zip.AddEntry("content/net45/Scripts/test2.js", new byte[] { 0 });
                zip.AddEntry("content/net451/Scripts/test3.js", new byte[] { 0 });

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

            return result;
        }

        public static FileInfo GetLegacyContentPackageMixed()
        {
            var file = Path.GetTempFileName() + ".nupkg";
            var result = new FileInfo(file);

            using (var zip = new ZipArchive(File.Create(result.FullName), ZipArchiveMode.Create))
            {
                zip.AddEntry("content/Scripts/test.js", new byte[] { 0 });
                zip.AddEntry("content/net40/Scripts/test2.js", new byte[] { 0 });
                zip.AddEntry("content/net40/Scripts/testb.js", new byte[] { 0 });
                zip.AddEntry("content/net45/Scripts/test3.js", new byte[] { 0 });

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

            return result;
        }
    }
}
