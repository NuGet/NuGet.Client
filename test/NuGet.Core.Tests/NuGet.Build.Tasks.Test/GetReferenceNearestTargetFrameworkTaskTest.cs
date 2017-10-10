// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Build.Framework;
using Moq;
using Xunit;

namespace NuGet.Build.Tasks.Test
{
    public class GetReferenceNearestTargetFrameworkTaskTest
    {
        [Fact]
        public void GetReferenceNearestTargetFrameworkTask_NoReferences()
        {
            var buildEngine = new TestBuildEngine();
            var testLogger = buildEngine.TestLogger;

            var task = new GetReferenceNearestTargetFrameworkTask
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
        public void GetReferenceNearestTargetFrameworkTask_BadSourceTF()
        {
            var buildEngine = new TestBuildEngine();
            var testLogger = buildEngine.TestLogger;

            var references = new List<ITaskItem>();
            var reference = new Mock<ITaskItem>();
            reference.SetupGet(e => e.ItemSpec).Returns("a.csproj");
            references.Add(reference.Object);

            var task = new GetReferenceNearestTargetFrameworkTask
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
        public void GetReferenceNearestTargetFrameworkTask_NoCompatibleTargetTF()
        {
            var buildEngine = new TestBuildEngine();
            var testLogger = buildEngine.TestLogger;

            var references = new List<ITaskItem>();
            var reference = new Mock<ITaskItem>();
            reference.SetupGet(e => e.ItemSpec).Returns("a.csproj");
            reference.Setup(e => e.GetMetadata("TargetFrameworks")).Returns("net461");
            reference.Setup(e => e.GetMetadata("HasSingleTargetFramework")).Returns("true");
            references.Add(reference.Object);

            var task = new GetReferenceNearestTargetFrameworkTask
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
        public void GetReferenceNearestTargetFrameworkTask_NoTargetTF()
        {
            var buildEngine = new TestBuildEngine();
            var testLogger = buildEngine.TestLogger;

            var references = new List<ITaskItem>();
            var reference = new Mock<ITaskItem>();
            reference.SetupGet(e => e.ItemSpec).Returns("a.csproj");
            references.Add(reference.Object);

            var task = new GetReferenceNearestTargetFrameworkTask
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
        public void GetReferenceNearestTargetFrameworkTask_SingleTargetTF()
        {
            var buildEngine = new TestBuildEngine();
            var testLogger = buildEngine.TestLogger;

            var references = new List<ITaskItem>();
            var reference = new Mock<ITaskItem>();
            reference.SetupGet(e => e.ItemSpec).Returns("a.csproj");
            reference.Setup(e => e.GetMetadata("TargetFrameworks")).Returns("net46");
            reference.Setup(e => e.GetMetadata("HasSingleTargetFramework")).Returns("true");
            references.Add(reference.Object);

            var task = new GetReferenceNearestTargetFrameworkTask
            {
                BuildEngine = buildEngine,
                CurrentProjectTargetFramework = "net46",
                AnnotatedProjectReferences = references.ToArray()
            };

            var result = task.Execute();
            result.Should().BeTrue();

            task.AssignedProjects.Should().HaveCount(1);
            task.AssignedProjects[0].GetMetadata("NearestTargetFramework").Should().Be("net46");

            testLogger.Warnings.Should().Be(0);
            testLogger.Errors.Should().Be(0);
            testLogger.DebugMessages.Count.Should().Be(3);
        }

        [Fact]
        public void GetReferenceNearestTargetFrameworkTask_MultipleTargetTF()
        {
            var buildEngine = new TestBuildEngine();
            var testLogger = buildEngine.TestLogger;

            var references = new List<ITaskItem>();
            var reference = new Mock<ITaskItem>();
            reference.SetupGet(e => e.ItemSpec).Returns("a.csproj");
            reference.Setup(e => e.GetMetadata("TargetFrameworks")).Returns("net20;netstandard20");
            references.Add(reference.Object);

            var task = new GetReferenceNearestTargetFrameworkTask
            {
                BuildEngine = buildEngine,
                CurrentProjectTargetFramework = "net46",
                AnnotatedProjectReferences = references.ToArray()
            };

            var result = task.Execute();
            result.Should().BeTrue();

            task.AssignedProjects.Should().HaveCount(1);
            task.AssignedProjects[0].GetMetadata("NearestTargetFramework").Should().Be("net20");

            testLogger.Warnings.Should().Be(0);
            testLogger.Errors.Should().Be(0);
            testLogger.DebugMessages.Count.Should().Be(3);
        }

        [Fact]
        public void GetReferenceNearestTargetFrameworkTask_BadTargetTF()
        {
            var buildEngine = new TestBuildEngine();
            var testLogger = buildEngine.TestLogger;

            var references = new List<ITaskItem>();
            var reference = new Mock<ITaskItem>();
            reference.SetupGet(e => e.ItemSpec).Returns("a.csproj");
            reference.Setup(e => e.GetMetadata("TargetFrameworks")).Returns("abadframework");
            references.Add(reference.Object);

            var task = new GetReferenceNearestTargetFrameworkTask
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
