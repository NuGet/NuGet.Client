using NuGet.Packaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Packaging.Test
{
    public class NuspecReaderTests
    {
        private const string basicNuspec = @"<?xml version=""1.0""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                  <metadata>
                    <id>packageA</id>
                    <version>1.0.1-alpha</version>
                    <title>Package A</title>
                    <authors>ownera, ownerb</authors>
                    <owners>ownera, ownerb</owners>
                    <requireLicenseAcceptance>false</requireLicenseAcceptance>
                    <description>package A description.</description>
                    <language>en-US</language>
                    <references>
                      <reference file=""a.dll"" />
                    </references>
                    <dependencies>
                        <group targetFramework=""net40"">
                          <dependency id=""jQuery"" />
                          <dependency id=""WebActivator"" version=""1.1.0"" />
                          <dependency id=""PackageC"" version=""[1.1.0, 2.0.1)"" />
                        </group>
                        <group targetFramework=""wp8"">
                          <dependency id=""jQuery"" />
                        </group>
                    </dependencies>
                  </metadata>
                </package>";

        [Fact]
        public void NuspecReaderTests_Id()
        {
            NuspecReader reader = GetReader(basicNuspec);

            string id = reader.GetId();

            Assert.Equal("packageA", id);
        }

        [Fact]
        public void NuspecReaderTests_DependencyGroups()
        {
            NuspecReader reader = GetReader(basicNuspec);

            var dependencies = reader.GetDependencyGroups().ToList();

            Assert.Equal(2, dependencies.Count);
        }


        private static NuspecReader GetReader(string nuspec)
        {
            using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(nuspec)))
            {
                return new NuspecReader(stream);
            }
        }
    }
}
