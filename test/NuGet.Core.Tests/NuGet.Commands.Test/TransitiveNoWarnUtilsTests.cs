// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using Xunit;
using FluentAssertions;
using System.Diagnostics;

namespace NuGet.Commands.Test
{
    public class TransitiveNoWarnUtilsTests
    {

        // Tests for TransitiveNoWarnUtils.ExtractPathNoWarnProperties
        [Fact]
        public void ExtractPathNoWarnProperties_ReturnsEmptySetIfPathPropertiesAreNull()
        {
            // Arrange & Act
            var extractedNoWarnSet = TransitiveNoWarnUtils.ExtractPathNoWarnProperties(null, "test_id");

            // Assert
            extractedNoWarnSet.Should().NotBeNull();
            extractedNoWarnSet.Should().BeEmpty();
        }

        [Fact]
        public void ExtractPathNoWarnProperties_CorrectlyReadsProjectWideNoWarns()
        {
            // Arrange
            var noWarnSet = new HashSet<NuGetLogCode> { NuGetLogCode.NU1601, NuGetLogCode.NU1603 };
            var warningsAsErrorSet = new HashSet<NuGetLogCode> { };

            var projectWideWarningProperties = new WarningProperties(
                warningsAsErrors: warningsAsErrorSet,
                noWarn: noWarnSet,
                allWarningsAsErrors: false
                );

            var warningPropertiesCollection = new WarningPropertiesCollection(
               projectWideWarningProperties: projectWideWarningProperties,
               packageSpecificWarningProperties: null,
               projectFrameworks: null
                );

            // Act
            var extractedNoWarnSet = TransitiveNoWarnUtils.ExtractPathNoWarnProperties(warningPropertiesCollection, "test_id");

            // Assert
            extractedNoWarnSet.Should().NotBeNullOrEmpty();
            extractedNoWarnSet.Should().BeEquivalentTo(noWarnSet);
        }

        [Fact]
        public void ExtractPathNoWarnProperties_CorrectlyReadsPackageSpecificNoWarns()
        {
            // Arrange
            var packageId = "test_package";
            var framework = NuGetFramework.Parse("net461");
            var expectedNoWarnSet = new HashSet<NuGetLogCode> { NuGetLogCode.NU1603, NuGetLogCode.NU1605 };
            var noWarnSet = new HashSet<NuGetLogCode> { };
            var warningsAsErrorSet = new HashSet<NuGetLogCode> { };

            var projectWideWarningProperties = new WarningProperties(
                warningsAsErrors: warningsAsErrorSet,
                noWarn: noWarnSet,
                allWarningsAsErrors: false
                );

            var packageSpecificWarningProperties = new PackageSpecificWarningProperties();
            packageSpecificWarningProperties.Add(NuGetLogCode.NU1603, packageId, framework);
            packageSpecificWarningProperties.Add(NuGetLogCode.NU1605, packageId, framework);
            packageSpecificWarningProperties.Add(NuGetLogCode.NU1701, "other_package", framework);

            var warningPropertiesCollection = new WarningPropertiesCollection(
               projectWideWarningProperties: projectWideWarningProperties,
               packageSpecificWarningProperties: packageSpecificWarningProperties,
               projectFrameworks: null
                );

            // Act
            var extractedNoWarnSet = TransitiveNoWarnUtils.ExtractPathNoWarnProperties(warningPropertiesCollection, packageId);

            // Assert
            extractedNoWarnSet.Should().NotBeNullOrEmpty();
            extractedNoWarnSet.Should().BeEquivalentTo(expectedNoWarnSet);
        }


        [Fact]
        public void ExtractPathNoWarnProperties_CorrectlyReadsPackageSpecificAndProjectWideNoWarns()
        {
            // Arrange
            var packageId = "test_package";
            var framework = NuGetFramework.Parse("net461");
            var expectedNoWarnSet = new HashSet<NuGetLogCode> { NuGetLogCode.NU1601 , NuGetLogCode.NU1603, NuGetLogCode.NU1605 };
            var noWarnSet = new HashSet<NuGetLogCode> { NuGetLogCode.NU1601, NuGetLogCode.NU1605 };
            var warningsAsErrorSet = new HashSet<NuGetLogCode> { };

            var projectWideWarningProperties = new WarningProperties(
                warningsAsErrors: warningsAsErrorSet,
                noWarn: noWarnSet,
                allWarningsAsErrors: false
                );

            var packageSpecificWarningProperties = new PackageSpecificWarningProperties();
            packageSpecificWarningProperties.Add(NuGetLogCode.NU1603, packageId, framework);
            packageSpecificWarningProperties.Add(NuGetLogCode.NU1605, packageId, framework);
            packageSpecificWarningProperties.Add(NuGetLogCode.NU1701, "other_package", framework);

            var warningPropertiesCollection = new WarningPropertiesCollection(
               projectWideWarningProperties: projectWideWarningProperties,
               packageSpecificWarningProperties: packageSpecificWarningProperties,
               projectFrameworks: null
                );

            // Act
            var extractedNoWarnSet = TransitiveNoWarnUtils.ExtractPathNoWarnProperties(warningPropertiesCollection, packageId);

            // Assert
            extractedNoWarnSet.Should().NotBeNullOrEmpty();
            extractedNoWarnSet.Should().BeEquivalentTo(expectedNoWarnSet);
        }


        // Tests for TransitiveNoWarnUtils.TryMergeNullObjects
        [Fact]
        public void TryMergeNullObjects_ReturnsNullIfBothAreNull()
        {
            // Arrange
            object mergedObject;
            object first = null;
            object second = null;

            // Act
            var success = TransitiveNoWarnUtils.TryMergeNullObjects(first, second, out mergedObject);

            // Assert
            success.Should().BeTrue();
            mergedObject.Should().BeNull();
        }

        [Fact]
        public void TryMergeNullObjects_ReturnsFirstIfNotNull()
        {
            // Arrange
            object mergedObject;
            var first = new object();
            object second = null;

            // Act
            var success = TransitiveNoWarnUtils.TryMergeNullObjects(first, second, out mergedObject);

            // Assert
            success.Should().BeTrue();
            mergedObject.Should().Be(first);
        }

        [Fact]
        public void TryMergeNullObjects_ReturnsSecondIfNotNull()
        {
            // Arrange
            object mergedObject;
            object first = null;
            var second = new object();

            // Act
            var success = TransitiveNoWarnUtils.TryMergeNullObjects(first, second, out mergedObject);

            // Assert
            success.Should().BeTrue();
            mergedObject.Should().Be(second);
        }

        [Fact]
        public void TryMergeNullObjects_ReturnsFailureIfNoneNull()
        {
            // Arrange
            object mergedObject;
            var first = new object();
            var second = new object();

            // Act
            var success = TransitiveNoWarnUtils.TryMergeNullObjects(first, second, out mergedObject);

            // Assert
            success.Should().BeFalse();
            mergedObject.Should().BeNull();
        }

        // Tests for TransitiveNoWarnUtils.MergePackageSpecificWarningProperties
        [Fact]
        public void MergePackageSpecificWarningProperties_ReturnsNullIfBothAreNull()
        {
            // Arrange
            PackageSpecificWarningProperties first = null;
            PackageSpecificWarningProperties second = null;

            // Act
            var merged = TransitiveNoWarnUtils.MergePackageSpecificWarningProperties(first, second);

            // Assert
            merged.Should().BeNull();
        }

        [Fact]
        public void MergePackageSpecificWarningProperties_ReturnsFirstIfNotNull()
        {
            // Arrange
            var first = new PackageSpecificWarningProperties();
            PackageSpecificWarningProperties second = null;

            // Act
            var merged = TransitiveNoWarnUtils.MergePackageSpecificWarningProperties(first, second);

            // Assert
            merged.Should().NotBeNull();
            merged.Should().Be(first);
        }

        [Fact]
        public void MergePackageSpecificWarningProperties_ReturnsSecondIfNotNull()
        {
            // Arrange
            PackageSpecificWarningProperties first = null;
            var second = new PackageSpecificWarningProperties();

            // Act
            var merged = TransitiveNoWarnUtils.MergePackageSpecificWarningProperties(first, second);

            // Assert
            merged.Should().NotBeNull();
            merged.Should().Be(second);
        }

        [Fact]
        public void MergePackageSpecificWarningProperties_MergesEmptyCollections()
        {
            // Arrange
            var first = new PackageSpecificWarningProperties();
            var second = new PackageSpecificWarningProperties();

            // Act
            var merged = TransitiveNoWarnUtils.MergePackageSpecificWarningProperties(first, second);

            // Assert
            merged.Should().NotBeNull();
            merged.Properties.Should().BeNull();
        }

        [Fact]
        public void MergePackageSpecificWarningProperties_MergesNonEmptyCollections()
        {
            // Arrange
            var packageId1 = "test_id1";
            var packageId2 = "test_id2";
            var net461 = NuGetFramework.Parse("net461");
            var netcoreapp = NuGetFramework.Parse("netcoreapp2.0");
            var expectedResult = new PackageSpecificWarningProperties();
            expectedResult.AddRangeOfCodes(
                new List<NuGetLogCode> { NuGetLogCode.NU1601, NuGetLogCode.NU1605 },
                packageId1,
                net461);
            expectedResult.AddRangeOfFrameworks(
                NuGetLogCode.NU1701,
                packageId2,
                new List<NuGetFramework> { net461, netcoreapp });
            expectedResult.AddRangeOfFrameworks(
                NuGetLogCode.NU1701,
                packageId1,
                new List<NuGetFramework> { net461, netcoreapp });
            expectedResult.AddRangeOfFrameworks(
                NuGetLogCode.NU1701,
                packageId2,
                new List<NuGetFramework> { net461, netcoreapp });
            expectedResult.AddRangeOfFrameworks(
                NuGetLogCode.NU1604,
                packageId1,
                new List<NuGetFramework> { net461, netcoreapp });


            var first = new PackageSpecificWarningProperties();
            first.AddRangeOfCodes(
                new List<NuGetLogCode> { NuGetLogCode.NU1601, NuGetLogCode.NU1605 },
                packageId1,
                net461);
            first.AddRangeOfFrameworks(
                NuGetLogCode.NU1701,
                packageId2,
                new List<NuGetFramework> { net461, netcoreapp });
            first.AddRangeOfFrameworks(
                NuGetLogCode.NU1701,
                packageId1,
                new List<NuGetFramework> { netcoreapp });

            var second = new PackageSpecificWarningProperties();
            second.AddRangeOfFrameworks(
                NuGetLogCode.NU1701,
                packageId2,
                new List<NuGetFramework> { net461, netcoreapp });
            second.AddRangeOfFrameworks(
                NuGetLogCode.NU1604,
                packageId1,
                new List<NuGetFramework> { net461, netcoreapp });
            second.AddRangeOfFrameworks(
                NuGetLogCode.NU1701,
                packageId1,
                new List<NuGetFramework> { net461 });


            // Act
            var merged = TransitiveNoWarnUtils.MergePackageSpecificWarningProperties(first, second);

            // Assert
            merged.Should().NotBeNull();
            merged.Properties.Should().NotBeNull();
            merged.ShouldBeEquivalentTo(expectedResult);
        }

        // Tests for TransitiveNoWarnUtils.MergeProjectWideWarningProperties
        [Fact]
        public void MergeProjectWideWarningProperties_ReturnsNullIfBothAreNull()
        {
            // Arrange
            WarningProperties first = null;
            WarningProperties second = null;

            // Act
            var merged = TransitiveNoWarnUtils.MergeProjectWideWarningProperties(first, second);

            // Assert
            merged.Should().BeNull();
        }

        [Fact]
        public void MergeProjectWideWarningProperties_ReturnsFirstIfNotNull()
        {
            // Arrange
            var first = new WarningProperties();
            WarningProperties second = null;

            // Act
            var merged = TransitiveNoWarnUtils.MergeProjectWideWarningProperties(first, second);

            // Assert
            merged.Should().NotBeNull();
            merged.Should().Be(first);
        }

        [Fact]
        public void MergeProjectWideWarningProperties_ReturnsSecondIfNotNull()
        {
            // Arrange
            WarningProperties first = null;
            var second = new WarningProperties();

            // Act
            var merged = TransitiveNoWarnUtils.MergeProjectWideWarningProperties(first, second);

            // Assert
            merged.Should().NotBeNull();
            merged.Should().Be(second);
        }

        [Fact]
        public void MergeProjectWideWarningProperties_MergesEmptyCollections()
        {
            // Arrange
            var first = new WarningProperties();
            var second = new WarningProperties();

            // Act
            var merged = TransitiveNoWarnUtils.MergeProjectWideWarningProperties(first, second);

            // Assert
            merged.Should().NotBeNull();
            merged.AllWarningsAsErrors.Should().BeFalse();
            merged.WarningsAsErrors.Should().BeEmpty();
            merged.NoWarn.Should().BeEmpty();
        }

        [Theory]
        [InlineData("NU1603, NU1605", "NU1701", true, "", "", false, "NU1603, NU1605", "", true)]
        [InlineData( "", "", false, "NU1603, NU1605", "NU1701", true, "NU1603, NU1605", "", true)]
        [InlineData("NU1603, NU1701", "", false, "NU1603, NU1701", "", false, "NU1603, NU1701", "", false)]
        [InlineData("", "NU1603, NU1701", false, "", "NU1603, NU1701", false, "", "", false)]
        [InlineData("NU1601", "NU1602, NU1603", false, "NU1604", "NU1605, NU1701", false, "NU1601, NU1604", "", false)]
        [InlineData("NU1601", "NU1602, NU1603", true, "NU1604", "NU1605, NU1701", false, "NU1601, NU1604", "", true)]
        [InlineData("", "", false, "", "", false, "", "", false)]
        [InlineData("", "", true, "", "", false, "", "", true)]
        [InlineData("", "", false, "", "", true, "", "", true)]
        [InlineData("", "", true, "", "", true, "", "", true)]
        public void MergeProjectWideWarningProperties_MergesNonEmptyCollections(
            string firstNoWarn, string firstWarningsAsErrors, bool firstAllWarningsAsErrors,
            string secondNoWarn, string secondWarningsAsErrors, bool secondAllWarningsAsErrors,
            string expectedNoWarn, string expectedWarningsAsErrors, bool expectedAllWarningsAsErrors)
        {
            // Arrange
            var first = new WarningProperties(
                new HashSet<NuGetLogCode>(MSBuildRestoreUtility.GetNuGetLogCodes(firstWarningsAsErrors)),
                new HashSet<NuGetLogCode>(MSBuildRestoreUtility.GetNuGetLogCodes(firstNoWarn)),
                firstAllWarningsAsErrors);

            var second = new WarningProperties(
                new HashSet<NuGetLogCode>(MSBuildRestoreUtility.GetNuGetLogCodes(secondWarningsAsErrors)),
                new HashSet<NuGetLogCode>(MSBuildRestoreUtility.GetNuGetLogCodes(secondNoWarn)),
                secondAllWarningsAsErrors);

            var expected = new WarningProperties(
                new HashSet<NuGetLogCode>(MSBuildRestoreUtility.GetNuGetLogCodes(expectedWarningsAsErrors)),
                new HashSet<NuGetLogCode>(MSBuildRestoreUtility.GetNuGetLogCodes(expectedNoWarn)),
                expectedAllWarningsAsErrors);

            // Act
            var merged = TransitiveNoWarnUtils.MergeProjectWideWarningProperties(first, second);

            // Assert
            merged.Should().NotBeNull();
            merged.AllWarningsAsErrors.ShouldBeEquivalentTo(expected.AllWarningsAsErrors);
            merged.WarningsAsErrors.Should().BeEmpty();
            merged.NoWarn.ShouldBeEquivalentTo(expected.NoWarn);
            merged.ShouldBeEquivalentTo(expected);
        }

        // Tests for TransitiveNoWarnUtils.MergeWarningPropertiesCollection
        [Fact]
        public void MergeWarningPropertiesCollection_ReturnsNullIfBothAreNull()
        {
            // Arrange
            WarningPropertiesCollection first = null;
            WarningPropertiesCollection second = null;

            // Act
            var merged = TransitiveNoWarnUtils.MergeWarningPropertiesCollection(first, second);

            // Assert
            merged.Should().BeNull();
        }

        [Fact]
        public void MergeWarningPropertiesCollection_ReturnsFirstIfNotNull()
        {
            // Arrange
            var first = new WarningPropertiesCollection(
                    projectWideWarningProperties: null,
                    packageSpecificWarningProperties: null,
                    projectFrameworks: null
                );
            WarningPropertiesCollection second = null;

            // Act
            var merged = TransitiveNoWarnUtils.MergeWarningPropertiesCollection(first, second);

            // Assert
            merged.Should().NotBeNull();
            merged.Should().Be(first);
        }

        [Fact]
        public void MergeWarningPropertiesCollection_ReturnsSecondIfNotNull()
        {
            // Arrange
            WarningPropertiesCollection first = null;
            var second = new WarningPropertiesCollection(
                    projectWideWarningProperties: null,
                    packageSpecificWarningProperties: null,
                    projectFrameworks: null
                );

            // Act
            var merged = TransitiveNoWarnUtils.MergeWarningPropertiesCollection(first, second);

            // Assert
            merged.Should().NotBeNull();
            merged.Should().Be(second);
        }

        [Fact]
        public void MergeWarningPropertiesCollection_MergesEmptyCollections()
        {
            // Arrange
            var first = new WarningPropertiesCollection(
                    projectWideWarningProperties: null,
                    packageSpecificWarningProperties: null,
                    projectFrameworks: null
                );
            var second = new WarningPropertiesCollection(
                    projectWideWarningProperties: null,
                    packageSpecificWarningProperties: null,
                    projectFrameworks: null
                );

            // Act
            var merged = TransitiveNoWarnUtils.MergeWarningPropertiesCollection(first, second);

            // Assert
            merged.Should().NotBeNull();
            merged.ProjectWideWarningProperties.Should().BeNull();
            merged.PackageSpecificWarningProperties.Should().BeNull();
            merged.ProjectFrameworks.Should().BeEmpty();
        }

        [Fact]
        public void MergeWarningPropertiesCollection_MergesNonEmptyCollections1()
        {
            // Arrange
            var first = new WarningPropertiesCollection(
                    projectWideWarningProperties: null,
                    packageSpecificWarningProperties: null,
                    projectFrameworks: new List<NuGetFramework> { NuGetFramework.Parse("net461"), NuGetFramework.Parse("netcoreapp1.0") }.AsReadOnly()
                );
            var second = new WarningPropertiesCollection(
                    projectWideWarningProperties: null,
                    packageSpecificWarningProperties: null,
                    projectFrameworks: new List<NuGetFramework> { NuGetFramework.Parse("net45"), NuGetFramework.Parse("netcoreapp1.1") }.AsReadOnly()
                );

            // Act
            var merged = TransitiveNoWarnUtils.MergeWarningPropertiesCollection(first, second);

            // Assert
            merged.Should().NotBeNull();
            merged.ProjectWideWarningProperties.Should().BeNull();
            merged.PackageSpecificWarningProperties.Should().BeNull();
            merged.ProjectFrameworks.Should().BeEmpty();
        }

        [Fact]
        public void MergeWarningPropertiesCollection_MergesNonEmptyCollections2()
        {
            // Arrange
            var first = new WarningPropertiesCollection(
                    projectWideWarningProperties: null,
                    packageSpecificWarningProperties: null,
                    projectFrameworks: new List<NuGetFramework> { NuGetFramework.Parse("net461"), NuGetFramework.Parse("netcoreapp1.0") }.AsReadOnly()
                );
            var second = new WarningPropertiesCollection(
                    projectWideWarningProperties: null,
                    packageSpecificWarningProperties: null,
                    projectFrameworks: new List<NuGetFramework> { NuGetFramework.Parse("net45"), NuGetFramework.Parse("netcoreapp1.1") }.AsReadOnly()
                );

            // Act
            var merged = TransitiveNoWarnUtils.MergeWarningPropertiesCollection(first, second);

            // Assert
            merged.Should().NotBeNull();
            merged.ProjectWideWarningProperties.Should().BeNull();
            merged.PackageSpecificWarningProperties.Should().BeNull();
            merged.ProjectFrameworks.Should().BeEmpty();
        }

        // Tests for TransitiveNoWarnUtils.DependencyNode equality
        [Fact]
        public void DependencyNodeEqualitySucceeds_NodesAreNull()
        {
            // Arrange
            TransitiveNoWarnUtils.DependencyNode first = null;
            TransitiveNoWarnUtils.DependencyNode second = null;

            // Act
            var seen = new HashSet<TransitiveNoWarnUtils.DependencyNode>
            {
                first,
                second
            };

            // Assert
            seen.Count.Should().Be(1);
        }

        [Fact]
        public void DependencyNodeEqualitySucceeds_OneNodeIsNull()
        {
            // Arrange
            var first = new TransitiveNoWarnUtils.DependencyNode(
                id: "test",
                isProject: true,
                warningPropertiesCollection: new WarningPropertiesCollection(null, null, null));

            TransitiveNoWarnUtils.DependencyNode second = null;

            // Act
            var seen = new HashSet<TransitiveNoWarnUtils.DependencyNode>
            {
                first,
                second
            };

            // Assert
            seen.Count.Should().Be(2);
        }

        [Fact]
        public void DependencyNodeEqualitySucceeds_NodesAreSame()
        {
            // Arrange
            var first = new TransitiveNoWarnUtils.DependencyNode(
                id: "test",
                isProject: true,
                warningPropertiesCollection: new WarningPropertiesCollection(null, null, null));

            // Act
            var seen = new HashSet<TransitiveNoWarnUtils.DependencyNode>
            {
                first                
            };

            // Assert
            seen.Count.Should().Be(1);
        }

        [Fact]
        public void DependencyNodeEqualitySucceeds_NodesHaveSameInternalObjects()
        {
            // Arrange
            var projectWideWarningProperties = new WarningProperties();
            var packageSpecificWarningProperties = new PackageSpecificWarningProperties();
            var projectFrameworks = new List<NuGetFramework>();

            var first = new TransitiveNoWarnUtils.DependencyNode(
                id: "test",
                isProject: true,
                warningPropertiesCollection: new WarningPropertiesCollection(
                   projectWideWarningProperties,
                   packageSpecificWarningProperties,
                   projectFrameworks
                ));

            var second = new TransitiveNoWarnUtils.DependencyNode(
                id: "test",
                isProject: true,
                warningPropertiesCollection: new WarningPropertiesCollection(
                   projectWideWarningProperties,
                   packageSpecificWarningProperties,
                   projectFrameworks
                ));

            // Act
            var seen = new HashSet<TransitiveNoWarnUtils.DependencyNode>
            {
                first,
                second
            };

            // Assert
            seen.Count.Should().Be(1);
        }

        [Theory]
        [InlineData("NU1605, NU1701", "NU1602, NU1603", false, "NU1701, NU1605", "NU1603, NU1602", false)]
        [InlineData("NU1605, NU1701", "NU1602, NU1603", true, "NU1701, NU1605", "NU1603, NU1602", true)]
        [InlineData("", "", false, "", "", false)]
        [InlineData("", "", true, "", "", true)]
        public void DependencyNodeEqualitySucceeds_NodesHaveEquivalentWarningProperties(
            string firstNoWarn, string firstWarningsAsErrors, bool firstAllWarningsAsErrors,
            string secondNoWarn, string secondWarningsAsErrors, bool secondAllWarningsAsErrors)
        {
            // Arrange
            var packageId1 = "test_id1";
            var packageId2 = "test_id2";
            var net461 = NuGetFramework.Parse("net461");
            var netcoreapp = NuGetFramework.Parse("netcoreapp2.0");

            // First
            var firstProjectWideWarningProperties = new WarningProperties(
                new HashSet<NuGetLogCode>(MSBuildRestoreUtility.GetNuGetLogCodes(firstWarningsAsErrors)),
                new HashSet<NuGetLogCode>(MSBuildRestoreUtility.GetNuGetLogCodes(firstNoWarn)),
                firstAllWarningsAsErrors);

            var firstPackageSpecificWarningProperties = new PackageSpecificWarningProperties();
            firstPackageSpecificWarningProperties.AddRangeOfCodes(
                new List<NuGetLogCode> { NuGetLogCode.NU1601, NuGetLogCode.NU1605 },
                packageId1,
                net461);
            firstPackageSpecificWarningProperties.AddRangeOfFrameworks(
                NuGetLogCode.NU1701,
                packageId2,
                new List<NuGetFramework> { net461, netcoreapp });
            firstPackageSpecificWarningProperties.AddRangeOfFrameworks(
                NuGetLogCode.NU1701,
                packageId1,
                new List<NuGetFramework> { net461 });

            var firstProjectFrameworks = new List<NuGetFramework> { net461, netcoreapp };

            // Second
            var secondProjectWideWarningProperties = new WarningProperties(
                new HashSet<NuGetLogCode>(MSBuildRestoreUtility.GetNuGetLogCodes(secondWarningsAsErrors)),
                new HashSet<NuGetLogCode>(MSBuildRestoreUtility.GetNuGetLogCodes(secondNoWarn)),
                secondAllWarningsAsErrors);

            var secondPackageSpecificWarningProperties = new PackageSpecificWarningProperties();
            secondPackageSpecificWarningProperties.AddRangeOfFrameworks(
                NuGetLogCode.NU1701,
                packageId1,
                new List<NuGetFramework> { net461 });
            secondPackageSpecificWarningProperties.AddRangeOfCodes(
                new List<NuGetLogCode> { NuGetLogCode.NU1701, NuGetLogCode.NU1605, NuGetLogCode.NU1601 },
                packageId1,
                net461);

            var secondProjectFrameworks = new List<NuGetFramework> { netcoreapp, net461 };

            var first = new TransitiveNoWarnUtils.DependencyNode(
                id: "test",
                isProject: true,
                warningPropertiesCollection: new WarningPropertiesCollection(
                   firstProjectWideWarningProperties,
                   firstPackageSpecificWarningProperties,
                   firstProjectFrameworks
                ));

            var second = new TransitiveNoWarnUtils.DependencyNode(
                id: "test",
                isProject: true,
                warningPropertiesCollection: new WarningPropertiesCollection(
                   firstProjectWideWarningProperties,
                   firstPackageSpecificWarningProperties,
                   firstProjectFrameworks
                ));

            // Act
            var seen = new HashSet<TransitiveNoWarnUtils.DependencyNode>
            {
                first,
                second
            };

            // Assert
            seen.Count.Should().Be(1);
        }

        [Fact]
        public void DependencyNodeEqualityFails_NodesHaveDifferentMetaData()
        {
            // Arrange
            var projectWideWarningProperties = new WarningProperties();
            var packageSpecificWarningProperties = new PackageSpecificWarningProperties();
            var projectFrameworks = new List<NuGetFramework>();

            var first = new TransitiveNoWarnUtils.DependencyNode(
                id: "test",
                isProject: true,
                warningPropertiesCollection: new WarningPropertiesCollection(
                   projectWideWarningProperties,
                   packageSpecificWarningProperties,
                   projectFrameworks
                ));

            var second = new TransitiveNoWarnUtils.DependencyNode(
                id: "test",
                isProject: false,
                warningPropertiesCollection: new WarningPropertiesCollection(
                   projectWideWarningProperties,
                   packageSpecificWarningProperties,
                   projectFrameworks
                ));

            var third = new TransitiveNoWarnUtils.DependencyNode(
                id: "test_other",
                isProject: true,
                warningPropertiesCollection: new WarningPropertiesCollection(
                   projectWideWarningProperties,
                   packageSpecificWarningProperties,
                   projectFrameworks
                ));

            // Act
            var seen = new HashSet<TransitiveNoWarnUtils.DependencyNode>
            {
                first,
                second,
                third
            };

            // Assert
            seen.Count.Should().Be(3);
        }

        [Theory]
        [InlineData("NU1605, NU1701", "", false, "", "", false)]
        [InlineData("NU1605, NU1701", "", false, "NU1701, NU1603", "", false)]
        [InlineData("", "NU1602, NU1603", true, "", "", true)]
        [InlineData("", "NU1602, NU1603", true, "", "NU1605, NU1602", true)]
        [InlineData("NU1605, NU1701", "NU1602, NU1603", true, "NU1701, NU1605", "NU1603, NU1602", false)]
        [InlineData("NU1605, NU1701", "NU1602, NU1603", false, "NU1701, NU1605", "NU1603, NU1602", true)]
        [InlineData("", "", true, "", "", false)]
        [InlineData("", "", false, "", "", true)]
        public void DependencyNodeEqualityFails_NodesHaveDifferentProjectWideWarningProperties(
            string firstNoWarn, string firstWarningsAsErrors, bool firstAllWarningsAsErrors,
            string secondNoWarn, string secondWarningsAsErrors, bool secondAllWarningsAsErrors)
        {
            // Arrange
            var packageSpecificWarningProperties = new PackageSpecificWarningProperties();
            var projectFrameworks = new List<NuGetFramework>();

            var firstProjectWideWarningProperties = new WarningProperties(
                new HashSet<NuGetLogCode>(MSBuildRestoreUtility.GetNuGetLogCodes(firstWarningsAsErrors)),
                new HashSet<NuGetLogCode>(MSBuildRestoreUtility.GetNuGetLogCodes(firstNoWarn)),
                firstAllWarningsAsErrors);

            var secondProjectWideWarningProperties = new WarningProperties(
                new HashSet<NuGetLogCode>(MSBuildRestoreUtility.GetNuGetLogCodes(secondWarningsAsErrors)),
                new HashSet<NuGetLogCode>(MSBuildRestoreUtility.GetNuGetLogCodes(secondNoWarn)),
                secondAllWarningsAsErrors);

            var first = new TransitiveNoWarnUtils.DependencyNode(
                id: "test",
                isProject: true,
                warningPropertiesCollection: new WarningPropertiesCollection(
                   firstProjectWideWarningProperties,
                   packageSpecificWarningProperties,
                   projectFrameworks
                ));

            var second = new TransitiveNoWarnUtils.DependencyNode(
                id: "test",
                isProject: true,
                warningPropertiesCollection: new WarningPropertiesCollection(
                   secondProjectWideWarningProperties,
                   packageSpecificWarningProperties,
                   projectFrameworks
                ));

            // Act
            var seen = new HashSet<TransitiveNoWarnUtils.DependencyNode>
            {
                first,
                second
            };

            // Assert
            seen.Count.Should().Be(2);
        }

        [Fact]
        public void DependencyNodeEqualityFails_NodesHaveDifferentFrameworksInPackageSpecificNoWarn()
        {
            // Arrange
            var packageId1 = "test_id1";
            var net461 = NuGetFramework.Parse("net461");
            var netcoreapp = NuGetFramework.Parse("netcoreapp2.0");

            var projectWideWarningProperties = new WarningProperties();
            var packageSpecificWarningProperties = new PackageSpecificWarningProperties();
            var projectFrameworks = new List<NuGetFramework> { net461, netcoreapp };

            var firstPackageSpecificWarningProperties = new PackageSpecificWarningProperties();
            firstPackageSpecificWarningProperties.AddRangeOfCodes(
                new List<NuGetLogCode> { NuGetLogCode.NU1601 },
                packageId1,
                net461);

            var secondPackageSpecificWarningProperties = new PackageSpecificWarningProperties();
            secondPackageSpecificWarningProperties.AddRangeOfCodes(
                new List<NuGetLogCode> { NuGetLogCode.NU1601 },
                packageId1,
                netcoreapp);

            var first = new TransitiveNoWarnUtils.DependencyNode(
                id: "test",
                isProject: true,
                warningPropertiesCollection: new WarningPropertiesCollection(
                   projectWideWarningProperties,
                   firstPackageSpecificWarningProperties,
                   projectFrameworks
                ));

            var second = new TransitiveNoWarnUtils.DependencyNode(
                id: "test",
                isProject: true,
                warningPropertiesCollection: new WarningPropertiesCollection(
                   projectWideWarningProperties,
                   secondPackageSpecificWarningProperties,
                   projectFrameworks
                ));

            // Act
            var seen = new HashSet<TransitiveNoWarnUtils.DependencyNode>
            {
                first,
                second
            };

            // Assert
            seen.Count.Should().Be(2);
        }

        [Fact]
        public void DependencyNodeEqualityFails_NodesHaveDifferentNoWarnCodesInPackageSpecificNoWarn()
        {
            // Arrange
            var packageId1 = "test_id1";
            var net461 = NuGetFramework.Parse("net461");

            var projectWideWarningProperties = new WarningProperties();
            var packageSpecificWarningProperties = new PackageSpecificWarningProperties();
            var projectFrameworks = new List<NuGetFramework> { net461 };

            var firstPackageSpecificWarningProperties = new PackageSpecificWarningProperties();
            firstPackageSpecificWarningProperties.AddRangeOfCodes(
                new List<NuGetLogCode> { NuGetLogCode.NU1601 },
                packageId1,
                net461);

            var secondPackageSpecificWarningProperties = new PackageSpecificWarningProperties();
            secondPackageSpecificWarningProperties.AddRangeOfCodes(
                new List<NuGetLogCode> { NuGetLogCode.NU1602 },
                packageId1,
                net461);

            var first = new TransitiveNoWarnUtils.DependencyNode(
                id: "test",
                isProject: true,
                warningPropertiesCollection: new WarningPropertiesCollection(
                   projectWideWarningProperties,
                   firstPackageSpecificWarningProperties,
                   projectFrameworks
                ));

            var second = new TransitiveNoWarnUtils.DependencyNode(
                id: "test",
                isProject: true,
                warningPropertiesCollection: new WarningPropertiesCollection(
                   projectWideWarningProperties,
                   secondPackageSpecificWarningProperties,
                   projectFrameworks
                ));

            // Act
            var seen = new HashSet<TransitiveNoWarnUtils.DependencyNode>
            {
                first,
                second
            };

            // Assert
            seen.Count.Should().Be(2);
        }

        [Fact]
        public void DependencyNodeEqualityFails_NodesHaveDifferentPackageIdsInPackageSpecificNoWarn()
        {
            // Arrange
            var packageId1 = "test_id1";
            var packageId2 = "test_id2";
            var net461 = NuGetFramework.Parse("net461");

            var projectWideWarningProperties = new WarningProperties();
            var packageSpecificWarningProperties = new PackageSpecificWarningProperties();
            var projectFrameworks = new List<NuGetFramework> { net461 };

            var firstPackageSpecificWarningProperties = new PackageSpecificWarningProperties();
            firstPackageSpecificWarningProperties.AddRangeOfCodes(
                new List<NuGetLogCode> { NuGetLogCode.NU1601 },
                packageId1,
                net461);

            var secondPackageSpecificWarningProperties = new PackageSpecificWarningProperties();
            secondPackageSpecificWarningProperties.AddRangeOfCodes(
                new List<NuGetLogCode> { NuGetLogCode.NU1601 },
                packageId2,
                net461);

            var first = new TransitiveNoWarnUtils.DependencyNode(
                id: "test",
                isProject: true,
                warningPropertiesCollection: new WarningPropertiesCollection(
                   projectWideWarningProperties,
                   firstPackageSpecificWarningProperties,
                   projectFrameworks
                ));

            var second = new TransitiveNoWarnUtils.DependencyNode(
                id: "test",
                isProject: true,
                warningPropertiesCollection: new WarningPropertiesCollection(
                   projectWideWarningProperties,
                   secondPackageSpecificWarningProperties,
                   projectFrameworks
                ));

            // Act
            var seen = new HashSet<TransitiveNoWarnUtils.DependencyNode>
            {
                first,
                second
            };

            // Assert
            seen.Count.Should().Be(2);
        }

        [Fact]
        public void DependencyNodeEqualityFails_NodesHaveDifferentProjectFrameworks()
        {
            // Arrange
            var packageId1 = "test_id1";
            var net461 = NuGetFramework.Parse("net461");
            var net45 = NuGetFramework.Parse("net45");
            var netcoreapp = NuGetFramework.Parse("netcoreapp1.0");

            var projectWideWarningProperties = new WarningProperties();
            var packageSpecificWarningProperties = new PackageSpecificWarningProperties();
            var firstProjectFrameworks = new List<NuGetFramework> { net461, netcoreapp };
            var secondProjectFrameworks = new List<NuGetFramework> { netcoreapp, net45, net461 };

            var firstPackageSpecificWarningProperties = new PackageSpecificWarningProperties();
            firstPackageSpecificWarningProperties.AddRangeOfCodes(
                new List<NuGetLogCode> { NuGetLogCode.NU1601 },
                packageId1,
                net461);

            var secondPackageSpecificWarningProperties = new PackageSpecificWarningProperties();
            secondPackageSpecificWarningProperties.AddRangeOfCodes(
                new List<NuGetLogCode> { NuGetLogCode.NU1601 },
                packageId1,
                net461);

            var first = new TransitiveNoWarnUtils.DependencyNode(
                id: "test",
                isProject: true,
                warningPropertiesCollection: new WarningPropertiesCollection(
                   projectWideWarningProperties,
                   firstPackageSpecificWarningProperties,
                   firstProjectFrameworks
                ));

            var second = new TransitiveNoWarnUtils.DependencyNode(
                id: "test",
                isProject: true,
                warningPropertiesCollection: new WarningPropertiesCollection(
                   projectWideWarningProperties,
                   secondPackageSpecificWarningProperties,
                   secondProjectFrameworks
                ));

            // Act
            var seen = new HashSet<TransitiveNoWarnUtils.DependencyNode>
            {
                first,
                second
            };

            // Assert
            seen.Count.Should().Be(2);
        }
    }
}
