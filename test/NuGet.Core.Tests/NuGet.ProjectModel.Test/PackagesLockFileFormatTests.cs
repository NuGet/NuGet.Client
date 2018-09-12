// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace NuGet.ProjectModel.Test
{
    public class PackagesLockFileFormatTests
    {
        [Fact]
        public void PackagesLockFileFormat_Read()
        {
            var nuGetLockFileContent = @"{
                ""version"": 1,
                ""dependencies"": {
                    "".NETFramework,Version=v4.5"": {
                        ""PackageA"": {
                            ""type"": ""Direct"",
                            ""requested"": ""[1.*, )"",
                            ""resolved"": ""1.0.0"",
                            ""sha512"": ""sbWWhjA2/cXJHBBKAVo3m2U0KxzNuW5dQANDwx8L96V+L6SML96cM/Myvmp6fiBqIDibvF6+Ss9YC+qqclrXnw=="",
                            ""dependencies"": {
                                 ""PackageB"": ""1.0.0""
                            }
                        },
                        ""PackageB"": {
                            ""type"": ""Transitive"",
                            ""resolved"": ""1.0.0"",
                            ""sha512"": ""Fjiywrwerewr4dgbdgbfgjkoiuiorwrwn24+8hjnnuerwrwsfsHYWD3HJYUI7NJHssxDFSFSFEWEW34DFDFCVsxv=="",
                        }
                    }
                }
            }";

            var lockFile = PackagesLockFileFormat.Parse(nuGetLockFileContent, "In Memory");

            Assert.Equal(1, lockFile.Targets.Count);

            var target = lockFile.Targets.First();
            Assert.Equal(".NETFramework,Version=v4.5", target.Name);
            Assert.Equal(2, target.Dependencies.Count);

            Assert.Equal("PackageA", target.Dependencies[0].Id);
            Assert.Equal(PackageDependencyType.Direct, target.Dependencies[0].Type);
            Assert.Equal("[1.*, )", target.Dependencies[0].RequestedVersion.ToNormalizedString());
            Assert.Equal("1.0.0", target.Dependencies[0].ResolvedVersion.ToNormalizedString());
            Assert.NotEmpty(target.Dependencies[0].Sha512);
            Assert.Equal(1, target.Dependencies[0].Dependencies.Count);
            Assert.Equal("PackageB", target.Dependencies[0].Dependencies[0].Id);


            Assert.Equal("PackageB", target.Dependencies[1].Id);
            Assert.Equal(PackageDependencyType.Transitive, target.Dependencies[1].Type);
            Assert.Null(target.Dependencies[1].RequestedVersion);
            Assert.Equal("1.0.0", target.Dependencies[0].ResolvedVersion.ToNormalizedString());
            Assert.NotEmpty(target.Dependencies[1].Sha512);
        }

        [Fact]
        public void PackagesLockFileFormat_ReadWithRuntimeGraph()
        {
            var nuGetLockFileContent = @"{
                ""version"": 1,
                ""dependencies"": {
                    "".NETFramework,Version=v4.5"": {
                        ""PackageA"": {
                            ""type"": ""Direct"",
                            ""requested"": ""[1.*, )"",
                            ""resolved"": ""1.0.0"",
                            ""sha512"": ""sbWWhjA2/cXJHBBKAVo3m2U0KxzNuW5dQANDwx8L96V+L6SML96cM/Myvmp6fiBqIDibvF6+Ss9YC+qqclrXnw=="",
                            ""dependencies"": {
                                 ""PackageB"": ""1.0.0""
                            }
                        },
                        ""PackageB"": {
                            ""type"": ""Transitive"",
                            ""resolved"": ""1.0.0"",
                            ""sha512"": ""Fjiywrwerewr4dgbdgbfgjkoiuiorwrwn24+8hjnnuerwrwsfsHYWD3HJYUI7NJHssxDFSFSFEWEW34DFDFCVsxv==""
                        }
                    },
                    "".NETFramework,Version=v4.5/win10-arm"": {
                        ""PackageA"": {
                            ""type"": ""Direct"",
                            ""requested"": ""[1.*, )"",
                            ""resolved"": ""1.0.0"",
                            ""sha512"": ""QuiokjhjA2/cXJHBBKAVo3m2U0KxzNuW5dQANDwx8L96V+L6SML96cM/Myvmp6fiBqIDibvF6+Ss9YC+qqcfwef=="",
                            ""dependencies"": {
                                 ""PackageB"": ""1.0.0"",
                                 ""runtime.win10-arm.PackageA"": ""1.0.0""
                            }
                        },
                        ""runtime.win10-arm.PackageA"": {
                            ""type"": ""Transitive"",
                            ""resolved"": ""1.0.0"",
                            ""sha512"": ""dfgdgdfIY434jhjkhkRARFSZSGFSDG423452bgdnuerwrwsfsHYWD3HJYUI7NJHssxDFSFSFEWEW34DFjkyuerd=="",
                        }
                    }
                }
            }";

            var lockFile = PackagesLockFileFormat.Parse(nuGetLockFileContent, "In Memory");

            Assert.Equal(2, lockFile.Targets.Count);

            var target = lockFile.Targets.First(t => !string.IsNullOrEmpty(t.RuntimeIdentifier));
            Assert.Equal(".NETFramework,Version=v4.5/win10-arm", target.Name);
            Assert.Equal(2, target.Dependencies.Count);

            Assert.Equal("PackageA", target.Dependencies[0].Id);
            Assert.Equal(PackageDependencyType.Direct, target.Dependencies[0].Type);
            Assert.Equal("[1.*, )", target.Dependencies[0].RequestedVersion.ToNormalizedString());
            Assert.Equal("1.0.0", target.Dependencies[0].ResolvedVersion.ToNormalizedString());
            Assert.NotEmpty(target.Dependencies[0].Sha512);
            Assert.Equal(2, target.Dependencies[0].Dependencies.Count);
            Assert.Equal("PackageB", target.Dependencies[0].Dependencies[0].Id);
            Assert.Equal("runtime.win10-arm.PackageA", target.Dependencies[0].Dependencies[1].Id);

            // Runtime graph will only have additional transitive dependenies which are not part of
            // original TFM graph
            Assert.Equal("runtime.win10-arm.PackageA", target.Dependencies[1].Id);
            Assert.Equal(PackageDependencyType.Transitive, target.Dependencies[1].Type);
            Assert.Null(target.Dependencies[1].RequestedVersion);
            Assert.Equal("1.0.0", target.Dependencies[0].ResolvedVersion.ToNormalizedString());
            Assert.NotEmpty(target.Dependencies[1].Sha512);
        }

        [Fact]
        public void PackagesLockFileFormat_Write()
        {
            var nuGetLockFileContent = @"{
                ""version"": 1,
                ""dependencies"": {
                    "".NETFramework,Version=v4.5"": {
                        ""PackageA"": {
                            ""type"": ""Direct"",
                            ""requested"": ""[1.*, )"",
                            ""resolved"": ""1.0.0"",
                            ""sha512"": ""sbWWhjA2/cXJHBBKAVo3m2U0KxzNuW5dQANDwx8L96V+L6SML96cM/Myvmp6fiBqIDibvF6+Ss9YC+qqclrXnw=="",
                            ""dependencies"": {
                                 ""PackageB"": ""1.0.0""
                            }
                        },
                        ""PackageB"": {
                            ""type"": ""Transitive"",
                            ""resolved"": ""1.0.0"",
                            ""sha512"": ""Fjiywrwerewr4dgbdgbfgjkoiuiorwrwn24+8hjnnuerwrwsfsHYWD3HJYUI7NJHssxDFSFSFEWEW34DFDFCVsxv=="",
                        }
                    }
                }
            }";

            var lockFile = PackagesLockFileFormat.Parse(nuGetLockFileContent, "In Memory");

            var output = JObject.Parse(PackagesLockFileFormat.Render(lockFile));
            var expected = JObject.Parse(nuGetLockFileContent);

            // Assert
            Assert.Equal(expected.ToString(), output.ToString());
        }
    }
}
