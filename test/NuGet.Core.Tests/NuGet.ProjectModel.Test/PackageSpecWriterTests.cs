using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.ProjectModel;
using Xunit;

namespace NuGet.ProjectModel.Test
{
    public class PackageSpecWriterTests
    {
        [Fact]
        public void PackageSpecReader_ProjectMissingVersion()
        {
            // Arrange
            var json = @"{
                          ""dependencies"": {
                                ""packageA"": {
                                    ""target"": ""project""
                                }
                            },
                            ""frameworks"": {
                                ""net46"": {}
                            }
                        }";

            // Act
            JsonPackageSpecReader.
        }
    }
}
