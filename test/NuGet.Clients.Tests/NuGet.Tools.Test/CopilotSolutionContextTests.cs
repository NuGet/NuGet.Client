// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.PackageManagement.VisualStudio;
using NuGet.VisualStudio;
using NuGetVSExtension;
using Xunit;

namespace NuGet.Tools.Test
{
    public class CopilotSolutionContextTests
    {
        [Fact]
        public async Task CreateAsync_IncludesSolutionDirectoryAndSortedDistinctProjectPaths()
        {
            string solutionDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            string projectDirectory = Path.Combine(solutionDirectory, "src", "App");
            string secondProjectDirectory = Path.Combine(solutionDirectory, "src", "Library");
            string projectPath = Path.Combine(projectDirectory, "App.csproj");
            string secondProjectPath = Path.Combine(secondProjectDirectory, "Library.csproj");

            try
            {
                Directory.CreateDirectory(projectDirectory);
                Directory.CreateDirectory(secondProjectDirectory);
                File.WriteAllText(projectPath, string.Empty);
                File.WriteAllText(secondProjectPath, string.Empty);

                var project = new Mock<IVsProjectAdapter>();
                project.SetupGet(adapter => adapter.FullProjectPath).Returns(projectPath);
                var duplicateProject = new Mock<IVsProjectAdapter>();
                duplicateProject.SetupGet(adapter => adapter.FullProjectPath).Returns(projectPath);
                var secondProject = new Mock<IVsProjectAdapter>();
                secondProject.SetupGet(adapter => adapter.FullProjectPath).Returns(secondProjectPath);

                var solutionManager = new Mock<IVsSolutionManager>();
                solutionManager.SetupGet(manager => manager.SolutionDirectory).Returns(solutionDirectory);
                solutionManager.Setup(manager => manager.GetAllVsProjectAdaptersAsync())
                    .ReturnsAsync([secondProject.Object, duplicateProject.Object, project.Object]);

                string context = await CopilotSolutionContext.CreateAsync(
                    solutionManager.Object,
                    CancellationToken.None);
                using JsonDocument json = JsonDocument.Parse(context);
                JsonElement root = json.RootElement;

                Assert.Equal(solutionDirectory, root.GetProperty("solutionDirectory").GetString());
                Assert.Equal([projectPath, secondProjectPath], GetStringValues(root, "projectPaths"));
                Assert.Equal(2, root.EnumerateObject().Count());
            }
            finally
            {
                if (Directory.Exists(solutionDirectory))
                {
                    Directory.Delete(solutionDirectory, recursive: true);
                }
            }
        }

        private static string[] GetStringValues(JsonElement root, string propertyName)
        {
            return root.GetProperty(propertyName)
                .EnumerateArray()
                .Select(element => element.GetString()!)
                .ToArray();
        }
    }
}
