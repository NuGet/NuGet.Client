// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using Moq;
using NuGet.Frameworks;
using Xunit;

namespace NuGet.PackageManagement.VisualStudio.Test
{
    public class LegacyCSProjPackageReferenceProjectTests
    {
/*        [Fact]
        public void LCPRP_AssetsFileLocation()
        {
            // Arrange
            var testEnvDTEProjectAdapter = new Mock<IEnvDTEProjectAdapter>();
            testEnvDTEProjectAdapter.Setup(x => x.GetBaseIntermediatePath().Result).Returns(@"C:\foo\obj");

            var testProject = new LegacyCSProjPackageReferenceProject(testEnvDTEProjectAdapter.Object);

            // Act
            var assetsPath = testProject.AssetsFilePath;

            // Assert
            Assert.Equal(@"C:\foo\obj\project.assets.json", assetsPath);
        }

        [Fact]
        public void LCPRP_InstalledPackages()
        {
            // Arrange
            System.Diagnostics.Debugger.Launch();
            var testEnvDTEProjectAdapter = new Mock<IEnvDTEProjectAdapter>();
            var metadataElements1 = Array.CreateInstance(typeof(string), 3);
            metadataElements1.SetValue("IncludeAssets", 0);
            metadataElements1.SetValue("ExcludeAssets", 1);
            metadataElements1.SetValue("PrivateAssets", 2);
            var metadataValues1 = Array.CreateInstance(typeof(string), 3);
            metadataValues1.SetValue("None", 0);
            metadataValues1.SetValue("All", 1);
            metadataValues1.SetValue("Compile;Runtime;ContentFiles", 2);
            testEnvDTEProjectAdapter.Setup(x => x.GetLegacyCSProjPackageReferencesAsync(It.IsAny<Array>()))
                .ReturnsAsync(new LegacyCSProjPackageReference[] {
                    new LegacyCSProjPackageReference()
                    {
                        Name = "foo1",
                        Version = "1.0.0",
                        TargetNuGetFramework = new NuGetFramework("net45"),
                        MetadataElements = metadataElements1,
                        MetadataValues = metadataValues1
                    }
                });

            var testProject = new LegacyCSProjPackageReferenceProject(testEnvDTEProjectAdapter.Object);

            // Act
            var installedPackages = testProject.GetInstalledPackagesAsync(CancellationToken.None).Result;

            // Assert
            Assert.Equal(1, installedPackages.Count());
            Assert.Equal("foo1", installedPackages.ElementAt(0).PackageIdentity.Id);
            Assert.Equal("1.0.0", installedPackages.ElementAt(0).PackageIdentity.Version.ToString());
            //Add additional verification
        }*/
    }
}
