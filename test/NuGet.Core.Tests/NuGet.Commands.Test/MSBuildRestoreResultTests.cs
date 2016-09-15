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
            using (var randomProjectDirectory = TestFileSystemUtility.CreateRandomTestFolder())
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

                    var targets = new Dictionary<string, IList<string>>();
                    targets.Add(string.Empty, new List<string>() { "blah" });

                      var msBuildRestoreResult = new MSBuildRestoreResult(
                        targetsPath,
                        propsPath,
                        globalPackagesFolder,
                        new Dictionary<string, IList<string>>(),
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
        public void MSBuildRestoreResult_MultipleTFMs()
        {
            // Arrange
            using (var globalPackagesFolder = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomProjectDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var projectName = "testproject";

                // Only run the test if globalPackagesFolder can be determined
                // Because, globalPackagesFolder would be null if %USERPROFILE% was null

                var targetsName = $"{projectName}.nuget.g.targets";
                var targetsPath = Path.Combine(randomProjectDirectory, targetsName);

                var propsName = $"{projectName}.nuget.g.props";
                var propsPath = Path.Combine(randomProjectDirectory, propsName);

                var targets = new Dictionary<string, IList<string>>();
                targets.Add("net45", new List<string>() { "a.targets", "b.targets" });
                targets.Add("netstandard16", new List<string>() { "c.targets" });
                targets.Add("netStandard1.7", new List<string>() { });

                var props = new Dictionary<string, IList<string>>();
                props.Add("net45", new List<string>() { "a.props", "b.props" });
                props.Add("netstandard16", new List<string>() { "c.props" });
                props.Add("netStandard1.7", new List<string>() { });
                props.Add("netStandard1.8", new List<string>() { });

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
        public void MSBuildRestoreResult_SingleTFM()
        {
            // Arrange
            using (var globalPackagesFolder = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomProjectDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var projectName = "testproject";

                // Only run the test if globalPackagesFolder can be determined
                // Because, globalPackagesFolder would be null if %USERPROFILE% was null

                var targetsName = $"{projectName}.nuget.g.targets";
                var targetsPath = Path.Combine(randomProjectDirectory, targetsName);

                var propsName = $"{projectName}.nuget.g.props";
                var propsPath = Path.Combine(randomProjectDirectory, propsName);

                var targets = new Dictionary<string, IList<string>>();
                targets.Add("net45", new List<string>() { "a.targets", "b.targets" });

                var props = new Dictionary<string, IList<string>>();
                props.Add("net45", new List<string>() { "a.props", "b.props" });

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
            using (var globalPackagesFolder = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomProjectDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var projectName = "testproject";

                // Only run the test if globalPackagesFolder can be determined
                // Because, globalPackagesFolder would be null if %USERPROFILE% was null

                var targetsName = $"{projectName}.nuget.g.targets";
                var targetsPath = Path.Combine(randomProjectDirectory, targetsName);

                var propsName = $"{projectName}.nuget.g.props";
                var propsPath = Path.Combine(randomProjectDirectory, propsName);

                var targets = new Dictionary<string, IList<string>>();
                targets.Add("", new List<string>() { "a.targets", "b.targets" });

                var props = new Dictionary<string, IList<string>>();
                props.Add("", new List<string>() { "a.props", "b.props" });

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
    }
}
