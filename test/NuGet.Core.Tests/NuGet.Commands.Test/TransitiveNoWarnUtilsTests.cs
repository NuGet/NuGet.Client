// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using FluentAssertions;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using Xunit;

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
            var projectWideNoWarn = new HashSet<NuGetLogCode> { NuGetLogCode.NU1601, NuGetLogCode.NU1603 };
            var pathWarningProperties = new TransitiveNoWarnUtils.NodeWarningProperties(
                projectWideNoWarn,
                null);

            // Act
            var extractedNoWarnSet = TransitiveNoWarnUtils.ExtractPathNoWarnProperties(
                pathWarningProperties,
                "test_id");

            // Assert
            extractedNoWarnSet.Should().NotBeNullOrEmpty();
            extractedNoWarnSet.Should().BeEquivalentTo(projectWideNoWarn);
        }

        [Fact]
        public void ExtractPathNoWarnProperties_CorrectlyReadsPackageSpecificNoWarns()
        {
            // Arrange
            var packageId = "test_package";
            var framework = NuGetFramework.Parse("net461");
            var expectedNoWarnSet = new HashSet<NuGetLogCode> { NuGetLogCode.NU1603, NuGetLogCode.NU1605 };

            var pathWarningProperties = new TransitiveNoWarnUtils.NodeWarningProperties(
                null,
                new Dictionary<string, HashSet<NuGetLogCode>>
                {
                    {packageId, expectedNoWarnSet}
                });

            // Act
            var extractedNoWarnSet = TransitiveNoWarnUtils.ExtractPathNoWarnProperties(
                pathWarningProperties,
                packageId);

            // Assert
            extractedNoWarnSet.Should().NotBeNullOrEmpty();
            extractedNoWarnSet.Should().BeEquivalentTo(expectedNoWarnSet);
        }


        [Fact]
        public void ExtractPathNoWarnProperties_CorrectlyReadsPackageSpecificAndProjectWideNoWarns()
        {
            // Arrange
            var packageId = "test_package";
            var expectedNoWarnSet = new HashSet<NuGetLogCode> { NuGetLogCode.NU1601, NuGetLogCode.NU1603, NuGetLogCode.NU1605, NuGetLogCode.NU1107 };
            var projectWideNoWarnSet = new HashSet<NuGetLogCode> { NuGetLogCode.NU1601, NuGetLogCode.NU1605 };
            var packageSpecificNoWarnSet = new HashSet<NuGetLogCode> { NuGetLogCode.NU1603, NuGetLogCode.NU1107 };
            var otherPackageSpecificNoWarnSet = new HashSet<NuGetLogCode> { NuGetLogCode.NU1603, NuGetLogCode.NU1701 };

            var pathWarningProperties = new TransitiveNoWarnUtils.NodeWarningProperties(
                projectWideNoWarnSet,
                new Dictionary<string, HashSet<NuGetLogCode>>
                {
                    {packageId, packageSpecificNoWarnSet},
                    {"other_package", otherPackageSpecificNoWarnSet}
                });

            // Act
            var extractedNoWarnSet = TransitiveNoWarnUtils.ExtractPathNoWarnProperties(
                pathWarningProperties,
                packageId);

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
            Dictionary<string, HashSet<NuGetLogCode>> first = null;
            Dictionary<string, HashSet<NuGetLogCode>> second = null;

            // Act
            var merged = TransitiveNoWarnUtils.MergePackageSpecificNoWarn(first, second);

            // Assert
            merged.Should().BeNull();
        }

        [Fact]
        public void MergePackageSpecificWarningProperties_ReturnsFirstIfNotNull()
        {
            // Arrange
            var first = new Dictionary<string, HashSet<NuGetLogCode>>();
            Dictionary<string, HashSet<NuGetLogCode>> second = null;

            // Act
            var merged = TransitiveNoWarnUtils.MergePackageSpecificNoWarn(first, second);

            // Assert
            merged.Should().NotBeNull();
            merged.Should().BeSameAs(first);
        }

        [Fact]
        public void MergePackageSpecificWarningProperties_ReturnsSecondIfNotNull()
        {
            // Arrange
            Dictionary<string, HashSet<NuGetLogCode>> first = null;
            var second = new Dictionary<string, HashSet<NuGetLogCode>>();

            // Act
            var merged = TransitiveNoWarnUtils.MergePackageSpecificNoWarn(first, second);

            // Assert
            merged.Should().NotBeNull();
            merged.Should().BeSameAs(second);
        }

        [Fact]
        public void MergePackageSpecificWarningProperties_MergesEmptyCollections()
        {
            // Arrange
            var first = new Dictionary<string, HashSet<NuGetLogCode>>();
            var second = new Dictionary<string, HashSet<NuGetLogCode>>();

            // Act
            var merged = TransitiveNoWarnUtils.MergePackageSpecificNoWarn(first, second);

            // Assert
            merged.Should().NotBeNull();
            merged.Should().BeEmpty();
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


            var expectedNoWarnForNet461 = TransitiveNoWarnUtils.ExtractPackageSpecificNoWarnForFramework(expectedResult, net461);
            var expectedNoWarnForNetcoreapp = TransitiveNoWarnUtils.ExtractPackageSpecificNoWarnForFramework(expectedResult, netcoreapp);

            var firstNoWarnForNet461 = TransitiveNoWarnUtils.ExtractPackageSpecificNoWarnForFramework(first, net461);
            var firstNoWarnForNetcoreapp = TransitiveNoWarnUtils.ExtractPackageSpecificNoWarnForFramework(first, netcoreapp);

            var secondNoWarnForNet461 = TransitiveNoWarnUtils.ExtractPackageSpecificNoWarnForFramework(second, net461);
            var secondNoWarnForNetcoreapp = TransitiveNoWarnUtils.ExtractPackageSpecificNoWarnForFramework(second, netcoreapp);

            // Act
            var mergedNoWarnForNet461 = TransitiveNoWarnUtils.MergePackageSpecificNoWarn(firstNoWarnForNet461, secondNoWarnForNet461);
            var mergedNoWarnForNetcoreapp = TransitiveNoWarnUtils.MergePackageSpecificNoWarn(firstNoWarnForNetcoreapp, secondNoWarnForNetcoreapp);

            // Assert
            mergedNoWarnForNet461.Should().NotBeNull();
            mergedNoWarnForNet461.Should().BeEquivalentTo(expectedNoWarnForNet461);

            mergedNoWarnForNetcoreapp.Should().NotBeNull();
            mergedNoWarnForNetcoreapp.Should().BeEquivalentTo(expectedNoWarnForNetcoreapp);
        }

        // Tests for TransitiveNoWarnUtils.MergeProjectWideWarningProperties
        [Fact]
        public void MergeProjectWideWarningProperties_ReturnsNullIfBothAreNull()
        {
            // Arrange
            HashSet<NuGetLogCode> first = null;
            HashSet<NuGetLogCode> second = null;

            // Act
            var merged = TransitiveNoWarnUtils.MergeCodes(first, second);

            // Assert
            merged.Should().BeNull();
        }

        [Fact]
        public void MergeProjectWideWarningProperties_ReturnsFirstIfNotNull()
        {
            // Arrange
            var first = new HashSet<NuGetLogCode>();
            HashSet<NuGetLogCode> second = null;

            // Act
            var merged = TransitiveNoWarnUtils.MergeCodes(first, second);

            // Assert
            merged.Should().NotBeNull();
            merged.Should().BeSameAs(first);
        }

        [Fact]
        public void MergeProjectWideWarningProperties_ReturnsSecondIfNotNull()
        {
            // Arrange
            HashSet<NuGetLogCode> first = null;
            var second = new HashSet<NuGetLogCode>();

            // Act
            var merged = TransitiveNoWarnUtils.MergeCodes(first, second);

            // Assert
            merged.Should().NotBeNull();
            merged.Should().BeSameAs(second);
        }

        [Fact]
        public void MergeProjectWideWarningProperties_MergesEmptyCollections()
        {
            // Arrange
            var first = new HashSet<NuGetLogCode>();
            var second = new HashSet<NuGetLogCode>();

            // Act
            var merged = TransitiveNoWarnUtils.MergeCodes(first, second);

            // Assert
            merged.Should().NotBeNull();
            merged.Should().BeEmpty();
        }

        [Theory]
        [InlineData("", "", "")]
        [InlineData("NU1603, NU1605", "", "NU1603, NU1605")]
        [InlineData("", "NU1603, NU1605", "NU1603, NU1605")]
        [InlineData("NU1603, NU1605", "NU1603", "NU1603, NU1605")]
        [InlineData("NU1603, NU1605", "NU1701", "NU1603, NU1605, NU1701")]
        [InlineData("NU1603, NU1605", "NU1603, NU1107, NU1701", "NU1603, NU1605, NU1107, NU1701")]
        [InlineData("NU1605, NU1603", "NU1107, NU1701, NU1603", "NU1603, NU1605, NU1107, NU1701")]
        public void MergeProjectWideWarningProperties_MergesNonEmptyCollections(
            string firstNoWarn,
            string secondNoWarn,
            string expectedNoWarn)
        {
            // Arrange
            var first = new HashSet<NuGetLogCode>(MSBuildStringUtility.GetNuGetLogCodes(firstNoWarn));

            var second = new HashSet<NuGetLogCode>(MSBuildStringUtility.GetNuGetLogCodes(secondNoWarn));

            var expected = new HashSet<NuGetLogCode>(MSBuildStringUtility.GetNuGetLogCodes(expectedNoWarn));

            // Act
            var merged = TransitiveNoWarnUtils.MergeCodes(first, second);

            // Assert
            merged.Should().NotBeNull();
            merged.Should().BeEquivalentTo(expected);
        }


        // Tests for TransitiveNoWarnUtils.ExtractPackageSpecificNoWarnPerFramework
        [Fact]
        public void ExtractPackageSpecificNoWarnForFrameworks_NullInput()
        {
            // Arrange
            PackageSpecificWarningProperties input = null;

            // Act
            var result = TransitiveNoWarnUtils.ExtractPackageSpecificNoWarnPerFramework(input);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void ExtractPackageSpecificNoWarnForFrameworks_InputWithNullProperties()
        {
            // Arrange
            var input = new PackageSpecificWarningProperties();

            // Act
            var result = TransitiveNoWarnUtils.ExtractPackageSpecificNoWarnPerFramework(input);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void ExtractPackageSpecificNoWarnForFrameworks_InputWithProperties()
        {
            // Arrange
            var packageId1 = "test_id1";
            var packageId2 = "test_id2";
            var net461 = NuGetFramework.Parse("net461");
            var netcoreapp = NuGetFramework.Parse("netcoreapp2.0");
            var input = new PackageSpecificWarningProperties();
            input.AddRangeOfCodes(
                new List<NuGetLogCode> { NuGetLogCode.NU1601, NuGetLogCode.NU1605 },
                packageId1,
                net461);
            input.AddRangeOfFrameworks(
                NuGetLogCode.NU1701,
                packageId2,
                new List<NuGetFramework> { net461, netcoreapp });
            input.AddRangeOfFrameworks(
                NuGetLogCode.NU1701,
                packageId1,
                new List<NuGetFramework> { net461, netcoreapp });
            input.AddRangeOfFrameworks(
                NuGetLogCode.NU1701,
                packageId2,
                new List<NuGetFramework> { net461, netcoreapp });
            input.AddRangeOfFrameworks(
                NuGetLogCode.NU1604,
                packageId1,
                new List<NuGetFramework> { net461 });

            var expected = new Dictionary<NuGetFramework, Dictionary<string, HashSet<NuGetLogCode>>>
            {
                [net461] = new Dictionary<string, HashSet<NuGetLogCode>>(StringComparer.OrdinalIgnoreCase)
                {
                    [packageId1.ToUpper()] = new HashSet<NuGetLogCode>
                    {
                        NuGetLogCode.NU1601,
                        NuGetLogCode.NU1604,
                        NuGetLogCode.NU1605,
                        NuGetLogCode.NU1701,
                    },
                    [packageId2.ToUpper()] = new HashSet<NuGetLogCode>
                    {
                        NuGetLogCode.NU1701
                    }
                },
                [netcoreapp] = new Dictionary<string, HashSet<NuGetLogCode>>(StringComparer.OrdinalIgnoreCase)
                {
                    [packageId1.ToLower()] = new HashSet<NuGetLogCode>
                    {
                        NuGetLogCode.NU1701,
                    },
                    [packageId2.ToLower()] = new HashSet<NuGetLogCode>
                    {
                        NuGetLogCode.NU1701
                    }
                }
            };

            // Act
            var result = TransitiveNoWarnUtils.ExtractPackageSpecificNoWarnPerFramework(input);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(expected);
        }

        // Tests for TransitiveNoWarnUtils.ExtractPackageSpecificNoWarnForFramework
        [Fact]
        public void ExtractPackageSpecificNoWarnForFramework_NullInput()
        {
            // Arrange
            PackageSpecificWarningProperties input = null;
            NuGetFramework framework = null;

            // Act
            var result = TransitiveNoWarnUtils.ExtractPackageSpecificNoWarnForFramework(input, framework);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void ExtractPackageSpecificNoWarnForFramework_InputWithNullProperties()
        {
            // Arrange
            var input = new PackageSpecificWarningProperties();
            NuGetFramework framework = null;

            // Act
            var result = TransitiveNoWarnUtils.ExtractPackageSpecificNoWarnForFramework(input, framework);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void ExtractPackageSpecificNoWarnForFramework_InputWithProperties()
        {
            // Arrange

            var packageId1 = "test_id1";
            var packageId2 = "test_id2";
            var net461 = NuGetFramework.Parse("net461");
            var netcoreapp = NuGetFramework.Parse("netcoreapp2.0");
            var input = new PackageSpecificWarningProperties();
            input.AddRangeOfCodes(
                new List<NuGetLogCode> { NuGetLogCode.NU1601, NuGetLogCode.NU1605 },
                packageId1,
                net461);
            input.AddRangeOfFrameworks(
                NuGetLogCode.NU1701,
                packageId2,
                new List<NuGetFramework> { net461, netcoreapp });
            input.AddRangeOfFrameworks(
                NuGetLogCode.NU1701,
                packageId1,
                new List<NuGetFramework> { net461, netcoreapp });
            input.AddRangeOfFrameworks(
                NuGetLogCode.NU1701,
                packageId2,
                new List<NuGetFramework> { net461, netcoreapp });
            input.AddRangeOfFrameworks(
                NuGetLogCode.NU1604,
                packageId1,
                new List<NuGetFramework> { net461 });

            var expected = new Dictionary<NuGetFramework, Dictionary<string, HashSet<NuGetLogCode>>>
            {
                [net461] = new Dictionary<string, HashSet<NuGetLogCode>>(StringComparer.OrdinalIgnoreCase)
                {
                    [packageId1.ToUpper()] = new HashSet<NuGetLogCode>
                    {
                        NuGetLogCode.NU1601,
                        NuGetLogCode.NU1604,
                        NuGetLogCode.NU1605,
                        NuGetLogCode.NU1701,
                    },
                    [packageId2.ToUpper()] = new HashSet<NuGetLogCode>
                    {
                        NuGetLogCode.NU1701
                    }
                },
                [netcoreapp] = new Dictionary<string, HashSet<NuGetLogCode>>(StringComparer.OrdinalIgnoreCase)
                {
                    [packageId1.ToLower()] = new HashSet<NuGetLogCode>
                    {
                        NuGetLogCode.NU1701,
                    },
                    [packageId2.ToLower()] = new HashSet<NuGetLogCode>
                    {
                        NuGetLogCode.NU1701
                    }
                }
            };

            // Act
            var resultNet461 = TransitiveNoWarnUtils.ExtractPackageSpecificNoWarnForFramework(input, net461);
            var resultNetcoreapp = TransitiveNoWarnUtils.ExtractPackageSpecificNoWarnForFramework(input, netcoreapp);

            // Assert
            resultNet461.Should().NotBeNull();
            resultNet461.Should().BeEquivalentTo(expected[net461]);
            resultNetcoreapp.Should().NotBeNull();
            resultNetcoreapp.Should().BeEquivalentTo(expected[netcoreapp]);
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
                nodeWarningProperties: new TransitiveNoWarnUtils.NodeWarningProperties(null, null));

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
                nodeWarningProperties: new TransitiveNoWarnUtils.NodeWarningProperties(null, null));

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
            var projectWideNoWarn = new HashSet<NuGetLogCode>();
            var packageSpecificNoWarn = new Dictionary<string, HashSet<NuGetLogCode>>();

            var first = new TransitiveNoWarnUtils.DependencyNode(
                id: "test",
                isProject: true,
                nodeWarningProperties: new TransitiveNoWarnUtils.NodeWarningProperties(
                   projectWideNoWarn,
                   packageSpecificNoWarn
                ));

            var second = new TransitiveNoWarnUtils.DependencyNode(
                id: "test",
                isProject: true,
                nodeWarningProperties: new TransitiveNoWarnUtils.NodeWarningProperties(
                   projectWideNoWarn,
                   packageSpecificNoWarn
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
        public void DependencyNodeEqualitySucceeds_NodesHaveEquivalentWarningProperties()
        {
            // Arrange
            var packageId1 = "test_id1";
            var packageId2 = "test_id2";
            var net461 = NuGetFramework.Parse("net461");
            var netcoreapp = NuGetFramework.Parse("netcoreapp2.0");


            // Arrange
            var firstProjectWideNoWarn = new HashSet<NuGetLogCode> { NuGetLogCode.NU1601, NuGetLogCode.NU1603 };
            var firstPackageSpecificNoWarn = new Dictionary<string, HashSet<NuGetLogCode>>
            {
                    {packageId1, new HashSet<NuGetLogCode> { NuGetLogCode.NU1601, NuGetLogCode.NU1108 }},
                    {packageId2, new HashSet<NuGetLogCode> { NuGetLogCode.NU1601, NuGetLogCode.NU1701 }}
            };

            var secondProjectWideNoWarn = new HashSet<NuGetLogCode> { NuGetLogCode.NU1601, NuGetLogCode.NU1603 };
            var secondPackageSpecificNoWarn = new Dictionary<string, HashSet<NuGetLogCode>>
            {
                    {packageId1, new HashSet<NuGetLogCode> { NuGetLogCode.NU1601, NuGetLogCode.NU1108 }},
                    {packageId2, new HashSet<NuGetLogCode> { NuGetLogCode.NU1601, NuGetLogCode.NU1701 }}
            };

            var first = new TransitiveNoWarnUtils.DependencyNode(
                id: "test",
                isProject: true,
                nodeWarningProperties: new TransitiveNoWarnUtils.NodeWarningProperties(
                   firstProjectWideNoWarn,
                   firstPackageSpecificNoWarn
                ));

            var second = new TransitiveNoWarnUtils.DependencyNode(
                id: "test",
                isProject: true,
                nodeWarningProperties: new TransitiveNoWarnUtils.NodeWarningProperties(
                   secondProjectWideNoWarn,
                   secondPackageSpecificNoWarn
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
            var projectWideNoWarn = new HashSet<NuGetLogCode> { NuGetLogCode.NU1601, NuGetLogCode.NU1603 };
            var packageSpecificNoWarn = new Dictionary<string, HashSet<NuGetLogCode>>();

            var first = new TransitiveNoWarnUtils.DependencyNode(
                id: "test",
                isProject: true,
                nodeWarningProperties: new TransitiveNoWarnUtils.NodeWarningProperties(
                   projectWideNoWarn,
                   packageSpecificNoWarn
                ));

            var second = new TransitiveNoWarnUtils.DependencyNode(
                id: "test",
                isProject: false,
                nodeWarningProperties: new TransitiveNoWarnUtils.NodeWarningProperties(
                   projectWideNoWarn,
                   packageSpecificNoWarn
                ));

            var third = new TransitiveNoWarnUtils.DependencyNode(
                id: "test_other",
                isProject: true,
                nodeWarningProperties: new TransitiveNoWarnUtils.NodeWarningProperties(
                   projectWideNoWarn,
                   packageSpecificNoWarn
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
        [InlineData("NU1605, NU1701", "")]
        [InlineData("", "NU1605, NU1701")]
        [InlineData("NU1605, NU1701", "NU1604")]
        [InlineData("NU1604, NU1701", "NU1604")]
        [InlineData("NU1701", "NU1604, NU1701")]
        public void DependencyNodeEqualityFails_NodesHaveDifferentProjectWideWarningProperties(
            string firstNoWarn,
            string secondNoWarn)
        {
            // Arrange
            var packageId1 = "test_id1";
            var packageId2 = "test_id2";

            // Arrange
            var firstProjectWideNoWarn = new HashSet<NuGetLogCode>(MSBuildStringUtility.GetNuGetLogCodes(firstNoWarn));
            var firstPackageSpecificNoWarn = new Dictionary<string, HashSet<NuGetLogCode>>
            {
                    {packageId1, new HashSet<NuGetLogCode> { NuGetLogCode.NU1601, NuGetLogCode.NU1108 }},
                    {packageId2, new HashSet<NuGetLogCode> { NuGetLogCode.NU1601, NuGetLogCode.NU1701 }}
            };

            var secondProjectWideNoWarn = new HashSet<NuGetLogCode>(MSBuildStringUtility.GetNuGetLogCodes(secondNoWarn));
            var secondPackageSpecificNoWarn = new Dictionary<string, HashSet<NuGetLogCode>>
            {
                    {packageId1, new HashSet<NuGetLogCode> { NuGetLogCode.NU1601, NuGetLogCode.NU1108 }},
                    {packageId2, new HashSet<NuGetLogCode> { NuGetLogCode.NU1601, NuGetLogCode.NU1701 }}
            };

            var first = new TransitiveNoWarnUtils.DependencyNode(
                id: "test",
                isProject: true,
                nodeWarningProperties: new TransitiveNoWarnUtils.NodeWarningProperties(
                   firstProjectWideNoWarn,
                   firstPackageSpecificNoWarn
                ));

            var second = new TransitiveNoWarnUtils.DependencyNode(
                id: "test",
                isProject: true,
                nodeWarningProperties: new TransitiveNoWarnUtils.NodeWarningProperties(
                   secondProjectWideNoWarn,
                   secondPackageSpecificNoWarn
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
            var packageId2 = "test_id2";

            // Arrange
            var firstProjectWideNoWarn = new HashSet<NuGetLogCode> { NuGetLogCode.NU1601, NuGetLogCode.NU1108 };
            var firstPackageSpecificNoWarn = new Dictionary<string, HashSet<NuGetLogCode>>
            {
                    {packageId1, new HashSet<NuGetLogCode> { NuGetLogCode.NU1601, NuGetLogCode.NU1108 }},
                    {packageId2, new HashSet<NuGetLogCode> { NuGetLogCode.NU1601, NuGetLogCode.NU1701 }}
            };

            var secondProjectWideNoWarn = new HashSet<NuGetLogCode> { NuGetLogCode.NU1601, NuGetLogCode.NU1108 };
            var secondPackageSpecificNoWarn = new Dictionary<string, HashSet<NuGetLogCode>>
            {
                    {packageId1, new HashSet<NuGetLogCode> { NuGetLogCode.NU1601, NuGetLogCode.NU1701 }},
                    {packageId2, new HashSet<NuGetLogCode> { NuGetLogCode.NU1601, NuGetLogCode.NU1701 }}
            };

            var first = new TransitiveNoWarnUtils.DependencyNode(
                id: "test",
                isProject: true,
                nodeWarningProperties: new TransitiveNoWarnUtils.NodeWarningProperties(
                   firstProjectWideNoWarn,
                   firstPackageSpecificNoWarn
                ));

            var second = new TransitiveNoWarnUtils.DependencyNode(
                id: "test",
                isProject: true,
                nodeWarningProperties: new TransitiveNoWarnUtils.NodeWarningProperties(
                   secondProjectWideNoWarn,
                   secondPackageSpecificNoWarn
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

            // Arrange
            var firstProjectWideNoWarn = new HashSet<NuGetLogCode> { NuGetLogCode.NU1601, NuGetLogCode.NU1108 };
            var firstPackageSpecificNoWarn = new Dictionary<string, HashSet<NuGetLogCode>>
            {
                    {packageId1, new HashSet<NuGetLogCode> { NuGetLogCode.NU1601, NuGetLogCode.NU1701 }}
            };

            var secondProjectWideNoWarn = new HashSet<NuGetLogCode> { NuGetLogCode.NU1601, NuGetLogCode.NU1108 };
            var secondPackageSpecificNoWarn = new Dictionary<string, HashSet<NuGetLogCode>>
            {
                    {packageId2, new HashSet<NuGetLogCode> { NuGetLogCode.NU1601, NuGetLogCode.NU1701 }}
            };

            var first = new TransitiveNoWarnUtils.DependencyNode(
                id: "test",
                isProject: true,
                nodeWarningProperties: new TransitiveNoWarnUtils.NodeWarningProperties(
                   firstProjectWideNoWarn,
                   firstPackageSpecificNoWarn
                ));

            var second = new TransitiveNoWarnUtils.DependencyNode(
                id: "test",
                isProject: true,
                nodeWarningProperties: new TransitiveNoWarnUtils.NodeWarningProperties(
                   secondProjectWideNoWarn,
                   secondPackageSpecificNoWarn
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
