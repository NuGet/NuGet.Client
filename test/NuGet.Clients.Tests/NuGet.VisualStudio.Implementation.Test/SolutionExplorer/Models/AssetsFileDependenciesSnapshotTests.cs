using System.Linq;
using NuGet.ProjectModel;
using NuGet.VisualStudio.SolutionExplorer.Models;
using Xunit;

namespace NuGet.VisualStudio.Implementation.Test.SolutionExplorer.Models
{
    public class AssetsFileDependenciesSnapshotTests
    {
        [Fact]
        public void ParseLibraries_IgnoreCaseInDependenciesTree_Succeeds()
        {
            // Arrange
            var lockFileContent = @"{
  ""version"": 3,
  ""targets"": {
    ""net5.0"": {
      ""System.Runtime/4.0.20-beta-22927"": {
        ""type"": ""package"",
        ""dependencies"": {
          ""Frob"": ""4.0.20""
        },
        ""compile"": {
          ""ref/dotnet/System.Runtime.dll"": {}
        }
      }
    }
  },
  ""libraries"": {
    ""System.Runtime/4.0.20-beta-22927"": {
      ""sha512"": ""sup3rs3cur3"",
      ""type"": ""package"",
      ""files"": [
        ""System.Runtime.nuspec""
      ]
    }
  },
  ""projectFileDependencyGroups"": {
    """": [
      ""System.Runtime [4.0.10-beta-*, )""
    ],
    ""net5.0"": []
  },
  ""logs"": [
    {
      ""code"": ""NU1000"",
      ""level"": ""Error"",
      ""message"": ""test log message""
    }
  ]
}";
            var lockFileFormat = new LockFileFormat();
            var lockFile = lockFileFormat.Parse(lockFileContent, "In Memory");

            var dependencies = AssetsFileDependenciesSnapshot.ParseLibraries(lockFile.Targets.First());

            Assert.Equal(1, dependencies.Count);
            Assert.True(dependencies.ContainsKey("system.runtime"));
        }
    }
}
