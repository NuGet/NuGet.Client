// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Commands.Test
{
    public class MSBuildRestoreResultTests
    {
        [Fact]
        public void MSBuildRestoreResult_ReplaceWithUserProfileMacro()
        {
            // Arrange
            using (var randomProjectDirectory = TestDirectory.Create())
            {
                var projectName = "testproject";
                var globalPackagesFolder = SettingsUtility.GetGlobalPackagesFolder(NullSettings.Instance);

                if (!string.IsNullOrEmpty(globalPackagesFolder))
                {
                    // Only run the test if globalPackagesFolder can be determined
                    // Because, globalPackagesFolder would be null if %USERPROFILE% was null

                    var targetsName = $"{projectName}.nuget.targets";
                    var targetsPath = Path.Combine(randomProjectDirectory, targetsName);

                    var propsName = $"{projectName}.nuget.props";
                    var propsPath = Path.Combine(randomProjectDirectory, propsName);

                    var targets = new List<MSBuildRestoreImportGroup>();
                    targets.Add(new MSBuildRestoreImportGroup()
                    {
                        Imports = new List<string>() { "blah" }
                    });

                    var msBuildRestoreResult = new MSBuildRestoreResult(
                        targetsPath,
                        propsPath,
                        globalPackagesFolder,
                        new List<MSBuildRestoreImportGroup>(),
                        targets);

                    // Assert
                    Assert.False(File.Exists(targetsPath));

                    // Act
                    msBuildRestoreResult.Commit(Common.NullLogger.Instance);

                    Assert.True(File.Exists(targetsPath));
                    var xml = XDocument.Load(targetsPath);
                    var ns = XNamespace.Get("http://schemas.microsoft.com/developer/msbuild/2003");
                    var elements = xml.Root.Descendants(ns + "NuGetPackageRoot");
                    Assert.Single(elements);

                    var element = elements.Single();
                    string expected = null;

                    if (RuntimeEnvironmentHelper.IsWindows)
                    {
                        expected = Path.Combine(@"$(UserProfile)", ".nuget", "packages") + Path.DirectorySeparatorChar;
                    }
                    else
                    {
                        expected = Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".nuget", "packages") + Path.DirectorySeparatorChar;
                    }
                    Assert.Equal(expected, element.Value);
                }
            }
        }

        [Fact]
        public void MSBuildRestoreResult_MultipleTFMs_CrossTargeting()
        {
            // Arrange
            using (var globalPackagesFolder = TestDirectory.Create())
            using (var randomProjectDirectory = TestDirectory.Create())
            {
                var projectName = "testproject";

                var targetsName = $"{projectName}.nuget.g.targets";
                var targetsPath = Path.Combine(randomProjectDirectory, targetsName);

                var propsName = $"{projectName}.nuget.g.props";
                var propsPath = Path.Combine(randomProjectDirectory, propsName);

                var propGroups = new List<MSBuildRestoreImportGroup>();
                var targetGroups = new List<MSBuildRestoreImportGroup>();

                targetGroups.Add(new MSBuildRestoreImportGroup()
                {
                    Imports = new List<string>()
                    {
                        "a.targets", "b.targets"
                    },
                    Conditions = new List<string>()
                    {
                        "'$(TargetFramework)' == 'net45'"
                    }
                });

                targetGroups.Add(new MSBuildRestoreImportGroup()
                {
                    Imports = new List<string>()
                    {
                        "c.targets"
                    },
                    Conditions = new List<string>()
                    {
                        "'$(TargetFramework)' == 'netstandard16'"
                    }
                });

                targetGroups.Add(new MSBuildRestoreImportGroup()
                {
                    Conditions = new List<string>()
                    {
                        "'$(TargetFramework)' == 'netStandard1.7'"
                    }
                });

                targetGroups.Add(new MSBuildRestoreImportGroup()
                {
                    Imports = new List<string>()
                    {
                         "x.targets", "y.targets"
                    },
                    Conditions = new List<string>()
                    {
                        "'$(IsCrossTargetingBuild)' == 'true'"
                    },
                    Position = 0,
                });

                propGroups.Add(new MSBuildRestoreImportGroup()
                {
                    Imports = new List<string>()
                    {
                        "a.props", "b.props"
                    },
                    Conditions = new List<string>()
                    {
                        "'$(TargetFramework)' == 'net45'"
                    }
                });

                propGroups.Add(new MSBuildRestoreImportGroup()
                {
                    Imports = new List<string>()
                    {
                        "c.props"
                    },
                    Conditions = new List<string>()
                    {
                        "'$(TargetFramework)' == 'netstandard16'"
                    }
                });

                propGroups.Add(new MSBuildRestoreImportGroup()
                {
                    Conditions = new List<string>()
                    {
                        "'$(TargetFramework)' == 'netStandard1.7'"
                    }
                });

                propGroups.Add(new MSBuildRestoreImportGroup()
                {
                    Conditions = new List<string>()
                    {
                        "'$(TargetFramework)' == 'netStandard1.8'"
                    }
                });

                propGroups.Add(new MSBuildRestoreImportGroup()
                {
                    Imports = new List<string>()
                    {
                         "z.props"
                    },
                    Conditions = new List<string>()
                    {
                        "'$(IsCrossTargetingBuild)' == 'true'"
                    },
                    Position = 0,
                });

                var msBuildRestoreResult = new MSBuildRestoreResult(
                  targetsPath,
                  propsPath,
                  globalPackagesFolder,
                  propGroups,
                  targetGroups);

                // Act
                msBuildRestoreResult.Commit(Common.NullLogger.Instance);

                // Assert
                Assert.True(File.Exists(targetsPath));
                var targetsXML = XDocument.Load(targetsPath);

                Assert.True(File.Exists(propsPath));
                var propsXML = XDocument.Load(propsPath);

                var targetItemGroups = targetsXML.Root.Elements().Where(e => e.Name.LocalName == "ImportGroup").ToList();
                var propsItemGroups = propsXML.Root.Elements().Where(e => e.Name.LocalName == "ImportGroup").ToList();

                Assert.Equal(3, targetItemGroups.Count);
                Assert.Equal("'$(IsCrossTargetingBuild)' == 'true'", targetItemGroups[0].Attribute(XName.Get("Condition")).Value.Trim());
                Assert.Equal("'$(TargetFramework)' == 'net45'", targetItemGroups[1].Attribute(XName.Get("Condition")).Value.Trim());
                Assert.Equal("'$(TargetFramework)' == 'netstandard16'", targetItemGroups[2].Attribute(XName.Get("Condition")).Value.Trim());

                Assert.Equal(2, targetItemGroups[0].Elements().Count());
                Assert.Equal("x.targets", targetItemGroups[0].Elements().ToList()[0].Attribute(XName.Get("Project")).Value);
                Assert.Equal("y.targets", targetItemGroups[0].Elements().ToList()[1].Attribute(XName.Get("Project")).Value);

                Assert.Equal(2, targetItemGroups[1].Elements().Count());
                Assert.Equal("a.targets", targetItemGroups[1].Elements().ToList()[0].Attribute(XName.Get("Project")).Value);
                Assert.Equal("b.targets", targetItemGroups[1].Elements().ToList()[1].Attribute(XName.Get("Project")).Value);

                Assert.Equal(1, targetItemGroups[2].Elements().Count());
                Assert.Equal("c.targets", targetItemGroups[2].Elements().ToList()[0].Attribute(XName.Get("Project")).Value);

                Assert.Equal(3, propsItemGroups.Count);
                Assert.Equal("'$(IsCrossTargetingBuild)' == 'true'", propsItemGroups[0].Attribute(XName.Get("Condition")).Value.Trim());

                Assert.Equal(1, propsItemGroups[0].Elements().Count());
                Assert.Equal("z.props", propsItemGroups[0].Elements().ToList()[0].Attribute(XName.Get("Project")).Value);
            }
        }

        [Fact]
        public void MSBuildRestoreResult_MultipleTFMs_CrossTargeting_EmptyGroups()
        {
            // Arrange
            using (var globalPackagesFolder = TestDirectory.Create())
            using (var randomProjectDirectory = TestDirectory.Create())
            {
                var projectName = "testproject";

                var targetsName = $"{projectName}.nuget.g.targets";
                var targetsPath = Path.Combine(randomProjectDirectory, targetsName);

                var propsName = $"{projectName}.nuget.g.props";
                var propsPath = Path.Combine(randomProjectDirectory, propsName);

                var props = new List<MSBuildRestoreImportGroup>();
                var targets = new List<MSBuildRestoreImportGroup>();

                props.Add(new MSBuildRestoreImportGroup()
                {
                    Conditions = new List<string>()
                    {
                        "'$(TargetFramework)' == 'net45'"
                    }
                });

                props.Add(new MSBuildRestoreImportGroup()
                {
                    Conditions = new List<string>()
                    {
                        "'$(TargetFramework)' == 'netStandard1.7'"
                    }
                });

                props.Add(new MSBuildRestoreImportGroup()
                {
                    Conditions = new List<string>()
                    {
                        "'$(IsCrossTargetingBuild)' == 'true'"
                    }
                });

                targets.Add(new MSBuildRestoreImportGroup()
                {
                    Conditions = new List<string>()
                    {
                        "'$(TargetFramework)' == 'net45'"
                    }
                });

                targets.Add(new MSBuildRestoreImportGroup()
                {
                    Conditions = new List<string>()
                    {
                        "'$(TargetFramework)' == 'netStandard1.7'"
                    }
                });

                targets.Add(new MSBuildRestoreImportGroup()
                {
                    Conditions = new List<string>()
                    {
                        "'$(IsCrossTargetingBuild)' == 'true'"
                    }
                });

                var msBuildRestoreResult = new MSBuildRestoreResult(
                  targetsPath,
                  propsPath,
                  globalPackagesFolder,
                  props,
                  targets);

                // Act
                msBuildRestoreResult.Commit(Common.NullLogger.Instance);

                // Assert
                Assert.False(File.Exists(targetsPath));
                Assert.False(File.Exists(propsPath));
            }
        }

        [Fact]
        public void MSBuildRestoreResult_MultipleTFMs()
        {
            // Arrange
            using (var globalPackagesFolder = TestDirectory.Create())
            using (var randomProjectDirectory = TestDirectory.Create())
            {
                var projectName = "testproject";

                // Only run the test if globalPackagesFolder can be determined
                // Because, globalPackagesFolder would be null if %USERPROFILE% was null

                var targetsName = $"{projectName}.nuget.g.targets";
                var targetsPath = Path.Combine(randomProjectDirectory, targetsName);

                var propsName = $"{projectName}.nuget.g.props";
                var propsPath = Path.Combine(randomProjectDirectory, propsName);

                var props = new List<MSBuildRestoreImportGroup>();
                var targets = new List<MSBuildRestoreImportGroup>();

                targets.Add(new MSBuildRestoreImportGroup()
                {
                    Conditions = new List<string>()
                    {
                        "'$(TargetFramework)' == 'net45'"
                    },
                    Imports = new List<string>()
                    {
                        "a.targets", "b.targets"
                    }
                });

                targets.Add(new MSBuildRestoreImportGroup()
                {
                    Conditions = new List<string>()
                    {
                        "'$(TargetFramework)' == 'netstandard16'"
                    },
                    Imports = new List<string>()
                    {
                        "c.targets"
                    }
                });

                targets.Add(new MSBuildRestoreImportGroup()
                {
                    Conditions = new List<string>()
                    {
                        "'$(TargetFramework)' == 'netStandard1.7'"
                    }
                });

                props.Add(new MSBuildRestoreImportGroup()
                {
                    Conditions = new List<string>()
                    {
                        "'$(TargetFramework)' == 'net45'"
                    },
                    Imports = new List<string>()
                    {
                        "a.props", "b.props"
                    }
                });

                props.Add(new MSBuildRestoreImportGroup()
                {
                    Conditions = new List<string>()
                    {
                        "'$(TargetFramework)' == 'netstandard16'"
                    },
                    Imports = new List<string>()
                    {
                        "c.props"
                    }
                });

                props.Add(new MSBuildRestoreImportGroup()
                {
                    Conditions = new List<string>()
                    {
                        "'$(TargetFramework)' == 'netStandard1.7'"
                    }
                });

                props.Add(new MSBuildRestoreImportGroup()
                {
                    Conditions = new List<string>()
                    {
                        "'$(TargetFramework)' == 'netStandard1.8'"
                    }
                });

                var msBuildRestoreResult = new MSBuildRestoreResult(
                  targetsPath,
                  propsPath,
                  globalPackagesFolder,
                  props,
                  targets);

                // Act
                msBuildRestoreResult.Commit(Common.NullLogger.Instance);

                // Assert
                Assert.True(File.Exists(targetsPath));
                var targetsXML = XDocument.Load(targetsPath);

                Assert.True(File.Exists(propsPath));
                var propsXML = XDocument.Load(propsPath);

                var targetItemGroups = targetsXML.Root.Elements().Where(e => e.Name.LocalName == "ImportGroup").ToList();

                Assert.Equal(2, targetItemGroups.Count);
                Assert.Equal("'$(TargetFramework)' == 'net45'", targetItemGroups[0].Attribute(XName.Get("Condition")).Value.Trim());
                Assert.Equal("'$(TargetFramework)' == 'netstandard16'", targetItemGroups[1].Attribute(XName.Get("Condition")).Value.Trim());

                Assert.Equal(2, targetItemGroups[0].Elements().Count());
                Assert.Equal("a.targets", targetItemGroups[0].Elements().ToList()[0].Attribute(XName.Get("Project")).Value);
                Assert.Equal("b.targets", targetItemGroups[0].Elements().ToList()[1].Attribute(XName.Get("Project")).Value);

                Assert.Equal(1, targetItemGroups[1].Elements().Count());
                Assert.Equal("c.targets", targetItemGroups[1].Elements().ToList()[0].Attribute(XName.Get("Project")).Value);
            }
        }

        [Fact]
        public void MSBuildRestoreResult_EmptyResult()
        {
            // Arrange
            using (var globalPackagesFolder = TestDirectory.Create())
            using (var randomProjectDirectory = TestDirectory.Create())
            {
                var projectName = "testproject";

                var targetsName = $"{projectName}.nuget.g.targets";
                var targetsPath = Path.Combine(randomProjectDirectory, targetsName);

                var propsName = $"{projectName}.nuget.g.props";
                var propsPath = Path.Combine(randomProjectDirectory, propsName);

                var props = new List<MSBuildRestoreImportGroup>();
                var targets = new List<MSBuildRestoreImportGroup>();

                var msBuildRestoreResult = new MSBuildRestoreResult(
                  targetsPath,
                  propsPath,
                  globalPackagesFolder,
                  props,
                  targets);

                // Act
                msBuildRestoreResult.Commit(Common.NullLogger.Instance);

                // Assert
                Assert.False(File.Exists(targetsPath));
                Assert.False(File.Exists(propsPath));
            }
        }

        [Fact]
        public void MSBuildRestoreResult_SingleTFM()
        {
            // Arrange
            using (var globalPackagesFolder = TestDirectory.Create())
            using (var randomProjectDirectory = TestDirectory.Create())
            {
                var projectName = "testproject";

                // Only run the test if globalPackagesFolder can be determined
                // Because, globalPackagesFolder would be null if %USERPROFILE% was null

                var targetsName = $"{projectName}.nuget.g.targets";
                var targetsPath = Path.Combine(randomProjectDirectory, targetsName);

                var propsName = $"{projectName}.nuget.g.props";
                var propsPath = Path.Combine(randomProjectDirectory, propsName);

                var props = new List<MSBuildRestoreImportGroup>();
                var targets = new List<MSBuildRestoreImportGroup>();

                targets.Add(new MSBuildRestoreImportGroup()
                {
                    Conditions = new List<string>()
                    {
                        "'$(TargetFramework)' == 'net45'"
                    },
                    Imports = new List<string>()
                    {
                        "a.targets", "b.targets"
                    }
                });

                props.Add(new MSBuildRestoreImportGroup()
                {
                    Conditions = new List<string>()
                    {
                        "'$(TargetFramework)' == 'net45'"
                    },
                    Imports = new List<string>()
                    {
                        "a.props", "b.props"
                    }
                });

                var msBuildRestoreResult = new MSBuildRestoreResult(
                  targetsPath,
                  propsPath,
                  globalPackagesFolder,
                  props,
                  targets);

                // Act
                msBuildRestoreResult.Commit(Common.NullLogger.Instance);

                // Assert
                Assert.True(File.Exists(targetsPath));
                var targetsXML = XDocument.Load(targetsPath);

                Assert.True(File.Exists(propsPath));
                var propsXML = XDocument.Load(propsPath);

                var targetItemGroups = targetsXML.Root.Elements().Where(e => e.Name.LocalName == "ImportGroup").ToList();

                Assert.Equal(1, targetItemGroups.Count);
                Assert.Equal("'$(TargetFramework)' == 'net45'", targetItemGroups[0].Attribute(XName.Get("Condition")).Value.Trim());
                Assert.Equal(2, targetItemGroups[0].Elements().Count());
                Assert.Equal("a.targets", targetItemGroups[0].Elements().ToList()[0].Attribute(XName.Get("Project")).Value);
                Assert.Equal("b.targets", targetItemGroups[0].Elements().ToList()[1].Attribute(XName.Get("Project")).Value);
            }
        }

        [Fact]
        public void MSBuildRestoreResult_SingleTFM_NoConditionals()
        {
            // Arrange
            using (var globalPackagesFolder = TestDirectory.Create())
            using (var randomProjectDirectory = TestDirectory.Create())
            {
                var projectName = "testproject";

                // Only run the test if globalPackagesFolder can be determined
                // Because, globalPackagesFolder would be null if %USERPROFILE% was null

                var targetsName = $"{projectName}.nuget.g.targets";
                var targetsPath = Path.Combine(randomProjectDirectory, targetsName);

                var propsName = $"{projectName}.nuget.g.props";
                var propsPath = Path.Combine(randomProjectDirectory, propsName);

                var props = new List<MSBuildRestoreImportGroup>();
                var targets = new List<MSBuildRestoreImportGroup>();

                targets.Add(new MSBuildRestoreImportGroup()
                {
                    Imports = new List<string>()
                    {
                        "a.targets", "b.targets"
                    }
                });

                props.Add(new MSBuildRestoreImportGroup()
                {
                    Imports = new List<string>()
                    {
                        "a.props", "b.props"
                    }
                });

                var msBuildRestoreResult = new MSBuildRestoreResult(
                  targetsPath,
                  propsPath,
                  globalPackagesFolder,
                  props,
                  targets);

                // Act
                msBuildRestoreResult.Commit(Common.NullLogger.Instance);

                // Assert
                Assert.True(File.Exists(targetsPath));
                var targetsXML = XDocument.Load(targetsPath);

                Assert.True(File.Exists(propsPath));
                var propsXML = XDocument.Load(propsPath);

                var targetItemGroups = targetsXML.Root.Elements().Where(e => e.Name.LocalName == "ImportGroup").ToList();

                Assert.Equal(1, targetItemGroups.Count);
                Assert.Equal(0, targetItemGroups[0].Attributes().Count());
                Assert.Equal(2, targetItemGroups[0].Elements().Count());
                Assert.Equal("a.targets", targetItemGroups[0].Elements().ToList()[0].Attribute(XName.Get("Project")).Value);
                Assert.Equal("b.targets", targetItemGroups[0].Elements().ToList()[1].Attribute(XName.Get("Project")).Value);
            }
        }

        [Fact]
        public void MSBuildRestoreResult_VerifyPositionAndSortOrder()
        {
            // Arrange
            using (var globalPackagesFolder = TestDirectory.Create())
            using (var randomProjectDirectory = TestDirectory.Create())
            {
                var projectName = "testproject";
                var targetsName = $"{projectName}.nuget.g.targets";
                var targetsPath = Path.Combine(randomProjectDirectory, targetsName);

                var propsName = $"{projectName}.nuget.g.props";
                var propsPath = Path.Combine(randomProjectDirectory, propsName);

                var props = new List<MSBuildRestoreImportGroup>();
                var targets = new List<MSBuildRestoreImportGroup>();

                targets.Add(new MSBuildRestoreImportGroup()
                {
                    Conditions = new List<string>()
                    {
                        "b"
                    },
                    Imports = new List<string>()
                    {
                        "a.targets"
                    },
                    Position = 0
                });

                targets.Add(new MSBuildRestoreImportGroup()
                {
                    Conditions = new List<string>()
                    {
                        "a"
                    },
                    Imports = new List<string>()
                    {
                        "a.targets"
                    },
                    Position = 0
                });

                targets.Add(new MSBuildRestoreImportGroup()
                {
                    Conditions = new List<string>()
                    {
                        "z"
                    },
                    Imports = new List<string>()
                    {
                        "a.targets"
                    },
                    Position = -1
                });

                targets.Add(new MSBuildRestoreImportGroup()
                {
                    Conditions = new List<string>()
                    {
                        "x"
                    },
                    Imports = new List<string>()
                    {
                        "a.targets"
                    },
                    Position = 100
                });

                var msBuildRestoreResult = new MSBuildRestoreResult(
                  targetsPath,
                  propsPath,
                  globalPackagesFolder,
                  props,
                  targets);

                // Act
                msBuildRestoreResult.Commit(Common.NullLogger.Instance);

                // Assert
                Assert.True(File.Exists(targetsPath));
                var targetsXML = XDocument.Load(targetsPath);
                var targetItemGroups = targetsXML.Root.Elements().Where(e => e.Name.LocalName == "ImportGroup").ToList();

                Assert.Equal(4, targetItemGroups.Count);
                Assert.Equal("z", targetItemGroups[0].Attribute(XName.Get("Condition")).Value.Trim());
                Assert.Equal("a", targetItemGroups[1].Attribute(XName.Get("Condition")).Value.Trim());
                Assert.Equal("b", targetItemGroups[2].Attribute(XName.Get("Condition")).Value.Trim());
                Assert.Equal("x", targetItemGroups[3].Attribute(XName.Get("Condition")).Value.Trim());
            }
        }
    }
}
