// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using Microsoft.Build.Framework;
using Xunit;

namespace NuGet.Build.Tasks.Test
{
    public class GetRestoreProjectReferencesTaskTests
    {
        [Fact]
        public void Execute_WithPackMetadata_CopiesMetadataToRestoreGraphItem()
        {
            // Arrange
            var task = new GetRestoreProjectReferencesTask()
            {
                BuildEngine = new TestBuildEngine(),
                ParentProjectPath = Path.Combine("src", "ProjectA", "ProjectA.csproj"),
                ProjectUniqueName = "ProjectA",
                TargetFrameworks = "net10.0",
                ProjectReferences = new ITaskItem[]
                {
                    new MockTaskItem(Path.Combine("..", "ProjectB", "ProjectB.csproj"))
                    {
                        ["Pack"] = "true",
                        ["PackagePath"] = "analyzers/dotnet/cs"
                    }
                }
            };

            // Act
            var result = task.Execute();

            // Assert
            Assert.True(result, "Task failed");
            Assert.Single(task.RestoreGraphItems);
            Assert.Equal("true", task.RestoreGraphItems[0].GetMetadata("Pack"));
            Assert.Equal("analyzers/dotnet/cs", task.RestoreGraphItems[0].GetMetadata("PackagePath"));
        }
    }
}
