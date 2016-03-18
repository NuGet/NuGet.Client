using System.Linq;
using System.IO;
using NuGet.LibraryModel;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.ProjectModel.Test
{
    public class XProjUtilityTests
    {
        [Fact]
        public void XProjUtility_DependencyTargetProject()
        {
            // Arrange
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var json1 = @"{
                          ""dependencies"": { },
                            ""frameworks"": {
                                ""net46"": {
                                    ""dependencies"": {
                                        ""project2"": { ""version"": ""1.0.0"", ""target"": ""project"" }
                                   }
                                }
                            }
                        }";

                var json2 = @"{
                            ""frameworks"": {
                                ""net46"": {}
                            }
                        }";

                var proj1Folder = Path.Combine(workingDir, "project1");
                var proj2Folder = Path.Combine(workingDir, "project2");
                Directory.CreateDirectory(proj1Folder);
                Directory.CreateDirectory(proj2Folder);

                var path1 = Path.Combine(proj1Folder, "project.json");
                var path2 = Path.Combine(proj2Folder, "project.json");
                File.WriteAllText(path1, json1);
                File.WriteAllText(path2, json2);
                var xproj1 = Path.Combine(proj1Folder, "project1.xproj");
                var xproj2 = Path.Combine(proj2Folder, "project2.xproj");
                File.WriteAllText(xproj1, string.Empty);
                File.WriteAllText(xproj2, string.Empty);

                // Act
                var references = XProjUtility.GetProjectReferences(xproj1);
                var reference = references.FirstOrDefault();

                // Assert
                Assert.Equal(xproj2, reference);
            }
        }

        [Fact]
        public void XProjUtility_DependencyTargetPackage()
        {
            // Arrange
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var json1 = @"{
                          ""dependencies"": { }
                            },
                            ""frameworks"": {
                                ""net46"": {
                                    ""dependencies"": {
                                        ""project2"": { ""version"": ""1.0.0"", ""target"": ""package"" }
                                   }
                                }
                            }
                        }";

                var json2 = @"{
                            ""frameworks"": {
                                ""net46"": {}
                            }
                        }";

                var proj1Folder = Path.Combine(workingDir, "project1");
                var proj2Folder = Path.Combine(workingDir, "project2");
                Directory.CreateDirectory(proj1Folder);
                Directory.CreateDirectory(proj2Folder);

                var path1 = Path.Combine(proj1Folder, "project.json");
                var path2 = Path.Combine(proj2Folder, "project.json");
                File.WriteAllText(path1, json1);
                File.WriteAllText(path2, json2);
                var xproj1 = Path.Combine(proj1Folder, "project1.xproj");
                var xproj2 = Path.Combine(proj2Folder, "project2.xproj");
                File.WriteAllText(xproj1, string.Empty);
                File.WriteAllText(xproj2, string.Empty);

                // Act
                var references = XProjUtility.GetProjectReferences(xproj1);
                var reference = references.FirstOrDefault();

                // Assert
                Assert.Equal(0, references.Count());
            }
        }

        [Fact]
        public void XProjUtility_TFMDependency()
        {
            // Arrange
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var json1 = @"{
                          ""dependencies"": { },
                            ""frameworks"": {
                                ""net46"": {
                                    ""dependencies"": {
                                        ""project2"": ""1.0.0""
                                   }
                                }
                            }
                        }";

                var json2 = @"{
                            ""frameworks"": {
                                ""net46"": {}
                            }
                        }";

                var proj1Folder = Path.Combine(workingDir, "project1");
                var proj2Folder = Path.Combine(workingDir, "project2");
                Directory.CreateDirectory(proj1Folder);
                Directory.CreateDirectory(proj2Folder);

                var path1 = Path.Combine(proj1Folder, "project.json");
                var path2 = Path.Combine(proj2Folder, "project.json");
                File.WriteAllText(path1, json1);
                File.WriteAllText(path2, json2);
                var xproj1 = Path.Combine(proj1Folder, "project1.xproj");
                var xproj2 = Path.Combine(proj2Folder, "project2.xproj");
                File.WriteAllText(xproj1, string.Empty);
                File.WriteAllText(xproj2, string.Empty);

                // Act
                var references = XProjUtility.GetProjectReferences(xproj1);
                var reference = references.FirstOrDefault();

                // Assert
                Assert.Equal(xproj2, reference);
            }
        }

        [Fact]
        public void XProjUtility_RootDependency()
        {
            // Arrange
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var json1 = @"{
                          ""dependencies"": {
                                ""project2"": ""1.0.0""
                                }
                            },
                            ""frameworks"": {
                                ""net46"": {}
                            }
                        }";

                var json2 = @"{
                            ""frameworks"": {
                                ""net46"": {}
                            }
                        }";

                var proj1Folder = Path.Combine(workingDir, "project1");
                var proj2Folder = Path.Combine(workingDir, "project2");
                Directory.CreateDirectory(proj1Folder);
                Directory.CreateDirectory(proj2Folder);

                var path1 = Path.Combine(proj1Folder, "project.json");
                var path2 = Path.Combine(proj2Folder, "project.json");
                File.WriteAllText(path1, json1);
                File.WriteAllText(path2, json2);
                var xproj1 = Path.Combine(proj1Folder, "project1.xproj");
                var xproj2 = Path.Combine(proj2Folder, "project2.xproj");
                File.WriteAllText(xproj1, string.Empty);
                File.WriteAllText(xproj2, string.Empty);

                // Act
                var references = XProjUtility.GetProjectReferences(xproj1);
                var reference = references.FirstOrDefault();

                // Assert
                Assert.Equal(xproj2, reference);
            }
        }

        [Fact]
        public void XProjUtility_DependencyNotFound()
        {
            // Arrange
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var json1 = @"{
                          ""dependencies"": {
                                ""project2"": ""1.0.0""
                                }
                            },
                            ""frameworks"": {
                                ""net46"": {}
                            }
                        }";

                var proj1Folder = Path.Combine(workingDir, "project1");
                Directory.CreateDirectory(proj1Folder);

                var path1 = Path.Combine(proj1Folder, "project.json");
                File.WriteAllText(path1, json1);
                var xproj1 = Path.Combine(proj1Folder, "project1.xproj");
                File.WriteAllText(xproj1, string.Empty);

                // Act
                var references = XProjUtility.GetProjectReferences(xproj1);

                // Assert
                Assert.Equal(0, references.Count());
            }
        }
    }
}
