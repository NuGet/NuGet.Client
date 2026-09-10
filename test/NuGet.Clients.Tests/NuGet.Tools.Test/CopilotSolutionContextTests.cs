// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Newtonsoft.Json.Linq;
using NuGet.PackageManagement.VisualStudio;
using NuGet.ProjectManagement;
using NuGetVSExtension;
using Xunit;

namespace NuGet.Tools.Test
{
    public class CopilotSolutionContextTests
    {
        [Fact]
        public async Task CreateAsync_IncludesExactProjectAndConfigurationPaths()
        {
            string solutionDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            string projectDirectory = Path.Combine(solutionDirectory, "src", "App");
            string solutionFilePath = Path.Combine(solutionDirectory, "App.sln");
            string projectPath = Path.Combine(projectDirectory, "App.csproj");
            string nuGetConfigPath = Path.Combine(solutionDirectory, "NuGet.Config");
            string directoryPackagesPropsPath = Path.Combine(solutionDirectory, "Directory.Packages.props");
            string directoryBuildPropsPath = Path.Combine(solutionDirectory, "src", "Directory.Build.props");

            try
            {
                Directory.CreateDirectory(projectDirectory);
                File.WriteAllText(solutionFilePath, string.Empty);
                File.WriteAllText(projectPath, string.Empty);
                File.WriteAllText(nuGetConfigPath, string.Empty);
                File.WriteAllText(directoryPackagesPropsPath, string.Empty);
                File.WriteAllText(directoryBuildPropsPath, string.Empty);

                var project = new Mock<NuGetProject>(
                    new Dictionary<string, object>
                    {
                        [NuGetProjectMetadataKeys.FullPath] = projectPath,
                    });

                var solutionManager = new Mock<IVsSolutionManager>();
                solutionManager.SetupGet(manager => manager.SolutionDirectory).Returns(solutionDirectory);
                solutionManager.Setup(manager => manager.GetSolutionFilePathAsync()).ReturnsAsync(solutionFilePath);
                solutionManager.Setup(manager => manager.GetNuGetProjectsAsync()).ReturnsAsync([project.Object]);

                string context = await CopilotSolutionContext.CreateAsync(solutionManager.Object, CancellationToken.None);
                JObject json = JObject.Parse(context);

                Assert.Equal(solutionDirectory, json["solutionDirectory"]!.Value<string>());
                Assert.Equal(solutionFilePath, json["solutionFilePath"]!.Value<string>());
                Assert.Equal([projectPath], json["projectPaths"]!.Values<string>());
                Assert.Equal(
                    [directoryPackagesPropsPath, nuGetConfigPath, directoryBuildPropsPath],
                    json["otherMsbuildFilePaths"]!.Values<string>());
            }
            finally
            {
                if (Directory.Exists(solutionDirectory))
                {
                    Directory.Delete(solutionDirectory, recursive: true);
                }
            }
        }
    }
}
