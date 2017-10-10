// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Build.Framework;
using Moq;
using Xunit;

namespace NuGet.Build.Tasks.Test
{
    public class AssignReferencePropertiesTaskTest
    {
        [Fact]
        public void AssignReferencePropertiesTask_NoReferences()
        {
            var buildEngine = new TestBuildEngine();
            var testLogger = buildEngine.TestLogger;

            var task = new AssignReferencePropertiesTask
            {
                BuildEngine = buildEngine,
                CurrentProjectTargetFramework = "net46"
            };

            var result = task.Execute();
            result.Should().BeTrue();

            task.AssignedProjects.Should().BeNullOrEmpty();

            testLogger.Warnings.Should().Be(0);
            testLogger.Errors.Should().Be(0);
            testLogger.DebugMessages.Count.Should().Be(2);
        }

        [Fact]
        public void AssignReferencePropertiesTask_BadSourceTF()
        {
            var buildEngine = new TestBuildEngine();
            var testLogger = buildEngine.TestLogger;

            var references = new List<ITaskItem>();
            var reference = new Mock<ITaskItem>();
            reference.SetupGet(e => e.ItemSpec).Returns("a.csproj");
            references.Add(reference.Object);

            var task = new AssignReferencePropertiesTask
            {
                BuildEngine = buildEngine,
                CurrentProjectTargetFramework = "abadframework",
                AnnotatedProjectReferences = references.ToArray()
            };

            var result = task.Execute();
            result.Should().BeFalse();

            task.AssignedProjects.Should().BeNullOrEmpty();

            testLogger.Warnings.Should().Be(0);
            testLogger.Errors.Should().Be(1);
            testLogger.DebugMessages.Count.Should().Be(2);
        }

        [Fact]
        public void AssignReferencePropertiesTask_NoCompatibleTargetTF()
        {
            var buildEngine = new TestBuildEngine();
            var testLogger = buildEngine.TestLogger;

            var references = new List<ITaskItem>();
            var reference = new Mock<ITaskItem>();
            reference.SetupGet(e => e.ItemSpec).Returns("a.csproj");
            reference.Setup(e => e.GetMetadata("TargetFrameworks")).Returns("net461");
            reference.Setup(e => e.GetMetadata("HasSingleTargetFramework")).Returns("true");
            references.Add(reference.Object);

            var task = new AssignReferencePropertiesTask
            {
                BuildEngine = buildEngine,
                CurrentProjectTargetFramework = "net46",
                AnnotatedProjectReferences = references.ToArray()
            };

            var result = task.Execute();
            result.Should().BeFalse();

            task.AssignedProjects.Should().HaveCount(1);

            testLogger.Warnings.Should().Be(0);
            testLogger.Errors.Should().Be(1);
            testLogger.DebugMessages.Count.Should().Be(3);
        }

        [Fact]
        public void AssignReferencePropertiesTask_NoTargetTF()
        {
            var buildEngine = new TestBuildEngine();
            var testLogger = buildEngine.TestLogger;

            var references = new List<ITaskItem>();
            var reference = new Mock<ITaskItem>();
            reference.SetupGet(e => e.ItemSpec).Returns("a.csproj");
            references.Add(reference.Object);

            var task = new AssignReferencePropertiesTask
            {
                BuildEngine = buildEngine,
                CurrentProjectTargetFramework = "net46",
                AnnotatedProjectReferences = references.ToArray()
            };

            var result = task.Execute();
            result.Should().BeTrue();

            task.AssignedProjects.Should().HaveCount(1);

            testLogger.Warnings.Should().Be(0);
            testLogger.Errors.Should().Be(0);
            testLogger.DebugMessages.Count.Should().Be(3);
        }

        [Fact]
        public void AssignReferencePropertiesTask_SingleTargetTF()
        {
            var buildEngine = new TestBuildEngine();
            var testLogger = buildEngine.TestLogger;

            var references = new List<ITaskItem>();
            var reference = new Mock<ITaskItem>();
            reference.SetupGet(e => e.ItemSpec).Returns("a.csproj");
            reference.Setup(e => e.GetMetadata("TargetFrameworks")).Returns("net46");
            reference.Setup(e => e.GetMetadata("HasSingleTargetFramework")).Returns("true");
            references.Add(reference.Object);

            var task = new AssignReferencePropertiesTask
            {
                BuildEngine = buildEngine,
                CurrentProjectTargetFramework = "net46",
                AnnotatedProjectReferences = references.ToArray()
            };

            var result = task.Execute();
            result.Should().BeTrue();

            task.AssignedProjects.Should().HaveCount(1);
            task.AssignedProjects[0].GetMetadata("UndefineProperties").Should().Be("TargetFramework");
            task.AssignedProjects[0].GetMetadata("SkipGetTargetFrameworkProperties").Should().Be("true");

            testLogger.Warnings.Should().Be(0);
            testLogger.Errors.Should().Be(0);
            testLogger.DebugMessages.Count.Should().Be(3);
        }

        [Fact]
        public void AssignReferencePropertiesTask_MultipleTargetTF()
        {
            var buildEngine = new TestBuildEngine();
            var testLogger = buildEngine.TestLogger;

            var references = new List<ITaskItem>();
            var reference = new Mock<ITaskItem>();
            reference.SetupGet(e => e.ItemSpec).Returns("a.csproj");
            reference.Setup(e => e.GetMetadata("TargetFrameworks")).Returns("net20;netstandard20");
            references.Add(reference.Object);

            var task = new AssignReferencePropertiesTask
            {
                BuildEngine = buildEngine,
                CurrentProjectTargetFramework = "net46",
                AnnotatedProjectReferences = references.ToArray()
            };

            var result = task.Execute();
            result.Should().BeTrue();

            task.AssignedProjects.Should().HaveCount(1);
            task.AssignedProjects[0].GetMetadata("SetTargetFramework").Should().Be("TargetFramework=net20");
            task.AssignedProjects[0].GetMetadata("SkipGetTargetFrameworkProperties").Should().Be("true");

            testLogger.Warnings.Should().Be(0);
            testLogger.Errors.Should().Be(0);
            testLogger.DebugMessages.Count.Should().Be(3);
        }

        [Fact]
        public void AssignReferencePropertiesTask_BadTargetTF()
        {
            var buildEngine = new TestBuildEngine();
            var testLogger = buildEngine.TestLogger;

            var references = new List<ITaskItem>();
            var reference = new Mock<ITaskItem>();
            reference.SetupGet(e => e.ItemSpec).Returns("a.csproj");
            reference.Setup(e => e.GetMetadata("TargetFrameworks")).Returns("abadframework");
            references.Add(reference.Object);

            var task = new AssignReferencePropertiesTask
            {
                BuildEngine = buildEngine,
                CurrentProjectTargetFramework = "net46",
                AnnotatedProjectReferences = references.ToArray()
            };

            var result = task.Execute();
            result.Should().BeFalse();

            task.AssignedProjects.Should().HaveCount(1);

            testLogger.Warnings.Should().Be(0);
            testLogger.Errors.Should().Be(1);
            testLogger.DebugMessages.Count.Should().Be(3);
        }

    }
}
