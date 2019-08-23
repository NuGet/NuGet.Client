using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class NuGetSpecCommandTests
    {
        [Theory]
        [InlineData("spec -AssemblyPath x b a")]
        [InlineData("spec a b -Force")]
        [InlineData("spec a b -?")]
        public void SpecCommand_Failure_InvalidArguments(string cmd)
        {
            Util.TestCommandInvalidArguments(cmd);
        }

        [Fact]
        public void SpecCommand_NoProjectFile()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestDirectory.Create())
            {
                var r = CommandRunner.Run(
                    nugetexe,
                    workingDirectory,
                    "spec",
                    waitForExit: true);

                Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);

                var nuspec = File.ReadAllText(Path.Combine(workingDirectory, "Package.nuspec"));
                Assert.Equal($@"<?xml version=""1.0""?>
<package >
  <metadata>
    <id>Package</id>
    <version>1.0.0</version>
    <authors>{Environment.UserName}</authors>
    <owners>{Environment.UserName}</owners>
    <licenseUrl>http://LICENSE_URL_HERE_OR_DELETE_THIS_LINE</licenseUrl>
    <projectUrl>http://PROJECT_URL_HERE_OR_DELETE_THIS_LINE</projectUrl>
    <iconUrl>http://ICON_URL_HERE_OR_DELETE_THIS_LINE</iconUrl>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Package description</description>
    <releaseNotes>Summary of changes made in this release of the package.</releaseNotes>
    <copyright>Copyright 2019</copyright>
    <tags>Tag1 Tag2</tags>
    <dependencies>
      <dependency id=""SampleDependency"" version=""1.0"" />
    </dependencies>
  </metadata>
</package>", nuspec);
            }
        }

        [Fact]
        public void SpecCommand_NoProjectFile_WithArg()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestDirectory.Create())
            {
                var r = CommandRunner.Run(
                    nugetexe,
                    workingDirectory,
                    "spec Whatnot",
                    waitForExit: true);

                Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);

                var fileName = Path.Combine(workingDirectory, "Whatnot.nuspec");
                Assert.True(File.Exists(fileName));
                var nuspec = File.ReadAllText(fileName);
                Assert.Equal($@"<?xml version=""1.0""?>
<package >
  <metadata>
    <id>Whatnot</id>
    <version>1.0.0</version>
    <authors>{Environment.UserName}</authors>
    <owners>{Environment.UserName}</owners>
    <licenseUrl>http://LICENSE_URL_HERE_OR_DELETE_THIS_LINE</licenseUrl>
    <projectUrl>http://PROJECT_URL_HERE_OR_DELETE_THIS_LINE</projectUrl>
    <iconUrl>http://ICON_URL_HERE_OR_DELETE_THIS_LINE</iconUrl>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Package description</description>
    <releaseNotes>Summary of changes made in this release of the package.</releaseNotes>
    <copyright>Copyright 2019</copyright>
    <tags>Tag1 Tag2</tags>
    <dependencies>
      <dependency id=""SampleDependency"" version=""1.0"" />
    </dependencies>
  </metadata>
</package>", nuspec);
            }
        }

        [Fact]
        public void SpecCommand_WithProjectFile()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestDirectory.Create())
            {
                Util.CreateFile(
                    workingDirectory,
                    "Project.csproj",
                    @"<Project Sdk=""Microsoft.NET.Sdk"">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>netcoreapp3.0</TargetFramework>
  </PropertyGroup>

</Project>");

                var r = CommandRunner.Run(
                    nugetexe,
                    workingDirectory,
                    "spec",
                    waitForExit: true);

                Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);

                var fileName = Path.Combine(workingDirectory, "Project.nuspec");
                Assert.True(File.Exists(fileName));
                var nuspec = File.ReadAllText(fileName);
                Assert.Equal($@"<?xml version=""1.0""?>
<package >
  <metadata>
    <id>$id$</id>
    <version>$version$</version>
    <title>$title$</title>
    <authors>$author$</authors>
    <owners>$author$</owners>
    <licenseUrl>http://LICENSE_URL_HERE_OR_DELETE_THIS_LINE</licenseUrl>
    <projectUrl>http://PROJECT_URL_HERE_OR_DELETE_THIS_LINE</projectUrl>
    <iconUrl>http://ICON_URL_HERE_OR_DELETE_THIS_LINE</iconUrl>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>$description$</description>
    <releaseNotes>Summary of changes made in this release of the package.</releaseNotes>
    <copyright>Copyright 2019</copyright>
    <tags>Tag1 Tag2</tags>
  </metadata>
</package>", nuspec);
            }
        }
    }
}
