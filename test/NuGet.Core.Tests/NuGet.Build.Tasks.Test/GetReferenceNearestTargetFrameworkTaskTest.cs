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
        private const int DEBUG_MESSAGE_COUNT_INPUT = 3;
        private const int DEBUG_MESSAGE_COUNT_INPUT_OUTPUT = DEBUG_MESSAGE_COUNT_INPUT + 1;

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
            testLogger.DebugMessages.Count.Should().Be(DEBUG_MESSAGE_COUNT_INPUT);
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
            testLogger.DebugMessages.Count.Should().Be(DEBUG_MESSAGE_COUNT_INPUT);
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
            testLogger.DebugMessages.Count.Should().Be(DEBUG_MESSAGE_COUNT_INPUT_OUTPUT);
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
            testLogger.DebugMessages.Count.Should().Be(DEBUG_MESSAGE_COUNT_INPUT_OUTPUT);
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
            testLogger.DebugMessages.Count.Should().Be(DEBUG_MESSAGE_COUNT_INPUT_OUTPUT);
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
            testLogger.DebugMessages.Count.Should().Be(DEBUG_MESSAGE_COUNT_INPUT_OUTPUT);
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
            testLogger.DebugMessages.Count.Should().Be(DEBUG_MESSAGE_COUNT_INPUT_OUTPUT);
        }

        [Fact]
        public void GetReferenceNearestTargetFrameworkTask_BadAtf()
        {
            var buildEngine = new TestBuildEngine();
            var testLogger = buildEngine.TestLogger;

            var references = new List<ITaskItem>();
            var reference = new Mock<ITaskItem>();
            reference.SetupGet(e => e.ItemSpec).Returns("a.csproj");
            reference.Setup(e => e.GetMetadata("TargetFrameworks")).Returns("netcoreapp2.0");
            reference.Setup(e => e.GetMetadata("HasSingleTargetFramework")).Returns("true");
            references.Add(reference.Object);

            var task = new GetReferenceNearestTargetFrameworkTask
            {
                BuildEngine = buildEngine,
                CurrentProjectTargetFramework = "netcoreapp2.0",
                FallbackTargetFrameworks = new string[] { "abcdef" },
                AnnotatedProjectReferences = references.ToArray()
            };

            var result = task.Execute();

            result.Should().BeFalse();
            task.AssignedProjects.Should().BeNullOrEmpty();
            testLogger.Warnings.Should().Be(0);
            testLogger.Errors.Should().Be(1);
            testLogger.DebugMessages.Count.Should().Be(DEBUG_MESSAGE_COUNT_INPUT);
        }

        [Fact]
        public void GetReferenceNearestTargetFrameworkTask_BadMultipleAtf()
        {
            var buildEngine = new TestBuildEngine();
            var testLogger = buildEngine.TestLogger;

            var references = new List<ITaskItem>();
            var reference = new Mock<ITaskItem>();
            reference.SetupGet(e => e.ItemSpec).Returns("a.csproj");
            reference.Setup(e => e.GetMetadata("TargetFrameworks")).Returns("netcoreapp2.0");
            references.Add(reference.Object);

            var task = new GetReferenceNearestTargetFrameworkTask
            {
                BuildEngine = buildEngine,
                CurrentProjectTargetFramework = "netcoreapp2.0",
                FallbackTargetFrameworks = new string[] { "net46", "abcdef" },
                AnnotatedProjectReferences = references.ToArray()
            };

            var result = task.Execute();

            result.Should().BeFalse();
            task.AssignedProjects.Should().BeNullOrEmpty();
            testLogger.Warnings.Should().Be(0);
            testLogger.Errors.Should().Be(1);
            testLogger.DebugMessages.Count.Should().Be(DEBUG_MESSAGE_COUNT_INPUT);
        }

        [Theory]
        [InlineData("netcoreapp2.0", "netcoreapp2.0")]
        [InlineData("net46", "net46")]
        public void GetReferenceNearestTargetFrameworkTask_WithSingleTfmAndSingleAtf(string referenceProjectFramework, string nearestMatchingFramework)
        {
            var buildEngine = new TestBuildEngine();
            var testLogger = buildEngine.TestLogger;

            var references = new List<ITaskItem>();
            var reference = new Mock<ITaskItem>();
            reference.SetupGet(e => e.ItemSpec).Returns("a.csproj");
            reference.Setup(e => e.GetMetadata("TargetFrameworks")).Returns(referenceProjectFramework);
            reference.Setup(e => e.GetMetadata("HasSingleTargetFramework")).Returns("true");
            references.Add(reference.Object);

            var task = new GetReferenceNearestTargetFrameworkTask
            {
                BuildEngine = buildEngine,
                CurrentProjectTargetFramework = "netcoreapp2.0",
                FallbackTargetFrameworks = new string[] { "net46" },
                AnnotatedProjectReferences = references.ToArray()
            };

            var result = task.Execute();
            result.Should().BeTrue();

            task.AssignedProjects.Should().HaveCount(1);
            task.AssignedProjects[0].GetMetadata("NearestTargetFramework").Should().Be(nearestMatchingFramework);

            testLogger.Warnings.Should().Be(0);
            testLogger.Errors.Should().Be(0);
            testLogger.DebugMessages.Count.Should().Be(DEBUG_MESSAGE_COUNT_INPUT_OUTPUT);
        }

        [Theory]
        [InlineData("netcoreapp2.0", "netcoreapp2.0")]
        [InlineData("net46", "net46")]
        public void GetReferenceNearestTargetFrameworkTask_WithSingleTfmAndMultipleAtf(string referenceProjectFramework, string nearestMatchingFramework)
        {
            var buildEngine = new TestBuildEngine();
            var testLogger = buildEngine.TestLogger;

            var references = new List<ITaskItem>();
            var reference = new Mock<ITaskItem>();
            reference.SetupGet(e => e.ItemSpec).Returns("a.csproj");
            reference.Setup(e => e.GetMetadata("TargetFrameworks")).Returns(referenceProjectFramework);
            reference.Setup(e => e.GetMetadata("HasSingleTargetFramework")).Returns("true");
            references.Add(reference.Object);

            var task = new GetReferenceNearestTargetFrameworkTask
            {
                BuildEngine = buildEngine,
                CurrentProjectTargetFramework = "netcoreapp2.0",
                FallbackTargetFrameworks = new string[] { "net46", "net461" },
                AnnotatedProjectReferences = references.ToArray()
            };

            var result = task.Execute();
            result.Should().BeTrue();

            task.AssignedProjects.Should().HaveCount(1);
            task.AssignedProjects[0].GetMetadata("NearestTargetFramework").Should().Be(nearestMatchingFramework);

            testLogger.Warnings.Should().Be(0);
            testLogger.Errors.Should().Be(0);
            testLogger.DebugMessages.Count.Should().Be(DEBUG_MESSAGE_COUNT_INPUT_OUTPUT);
        }

        [Theory]
        [InlineData("netcoreapp2.0; netcoreapp1.0", "netcoreapp2.0")]
        [InlineData("net45; net46", "net46")]
        public void GetReferenceNearestTargetFrameworkTask_WithMultipleTfmAndSingleAtf(string referenceProjectFramework, string nearestMatchingFramework)
        {
            var buildEngine = new TestBuildEngine();
            var testLogger = buildEngine.TestLogger;

            var references = new List<ITaskItem>();
            var reference = new Mock<ITaskItem>();
            reference.SetupGet(e => e.ItemSpec).Returns("a.csproj");
            reference.Setup(e => e.GetMetadata("TargetFrameworks")).Returns(referenceProjectFramework);
            references.Add(reference.Object);

            var task = new GetReferenceNearestTargetFrameworkTask
            {
                BuildEngine = buildEngine,
                CurrentProjectTargetFramework = "netcoreapp2.0",
                FallbackTargetFrameworks = new string[] { "net46" },
                AnnotatedProjectReferences = references.ToArray()
            };

            var result = task.Execute();
            result.Should().BeTrue();

            task.AssignedProjects.Should().HaveCount(1);
            task.AssignedProjects[0].GetMetadata("NearestTargetFramework").Should().Be(nearestMatchingFramework);

            testLogger.Warnings.Should().Be(0);
            testLogger.Errors.Should().Be(0);
            testLogger.DebugMessages.Count.Should().Be(DEBUG_MESSAGE_COUNT_INPUT_OUTPUT);
        }

        [Theory]
        [InlineData("netcoreapp2.0; netcoreapp1.0", "netcoreapp2.0")]
        [InlineData("net45; net46", "net46")]
        public void GetReferenceNearestTargetFrameworkTask_WithMultipleTfmAndMultipleAtf(string referenceProjectFramework, string nearestMatchingFramework)
        {
            var buildEngine = new TestBuildEngine();
            var testLogger = buildEngine.TestLogger;

            var references = new List<ITaskItem>();
            var reference = new Mock<ITaskItem>();
            reference.SetupGet(e => e.ItemSpec).Returns("a.csproj");
            reference.Setup(e => e.GetMetadata("TargetFrameworks")).Returns(referenceProjectFramework);
            references.Add(reference.Object);

            var task = new GetReferenceNearestTargetFrameworkTask
            {
                BuildEngine = buildEngine,
                CurrentProjectTargetFramework = "netcoreapp2.0",
                FallbackTargetFrameworks = new string[] { "net46", "net45", "net461" },
                AnnotatedProjectReferences = references.ToArray()
            };

            var result = task.Execute();
            result.Should().BeTrue();

            task.AssignedProjects.Should().HaveCount(1);
            task.AssignedProjects[0].GetMetadata("NearestTargetFramework").Should().Be(nearestMatchingFramework);

            testLogger.Warnings.Should().Be(0);
            testLogger.Errors.Should().Be(0);
            testLogger.DebugMessages.Count.Should().Be(DEBUG_MESSAGE_COUNT_INPUT_OUTPUT);
        }


        [Theory]
        [InlineData("netcoreapp2.0; netcoreapp1.1")]
        [InlineData("net45; net46")]
        public void GetReferenceNearestTargetFrameworkTask_WithMultipleTfmAndMultipleAtfAndNoMatch(string referenceProjectFramework)
        {
            var buildEngine = new TestBuildEngine();
            var testLogger = buildEngine.TestLogger;

            var references = new List<ITaskItem>();
            var reference = new Mock<ITaskItem>();
            reference.SetupGet(e => e.ItemSpec).Returns("a.csproj");
            reference.Setup(e => e.GetMetadata("TargetFrameworks")).Returns(referenceProjectFramework);
            references.Add(reference.Object);

            var task = new GetReferenceNearestTargetFrameworkTask
            {
                BuildEngine = buildEngine,
                CurrentProjectTargetFramework = "netcoreapp1.0",
                FallbackTargetFrameworks = new string[] { "net40", "net41" },
                AnnotatedProjectReferences = references.ToArray()
            };

            var result = task.Execute();
            result.Should().BeFalse();

            task.AssignedProjects.Should().HaveCount(1);

            testLogger.Warnings.Should().Be(0);
            testLogger.Errors.Should().Be(1);
            testLogger.DebugMessages.Count.Should().Be(DEBUG_MESSAGE_COUNT_INPUT_OUTPUT);
        }

        [Theory]
        [InlineData("netcoreapp2.0")]
        [InlineData("net46")]
        public void GetReferenceNearestTargetFrameworkTask_WithSingleTfmAndSingleAtfAndNoMatch(string referenceProjectFramework)
        {
            var buildEngine = new TestBuildEngine();
            var testLogger = buildEngine.TestLogger;

            var references = new List<ITaskItem>();
            var reference = new Mock<ITaskItem>();
            reference.SetupGet(e => e.ItemSpec).Returns("a.csproj");
            reference.Setup(e => e.GetMetadata("TargetFrameworks")).Returns(referenceProjectFramework);
            references.Add(reference.Object);

            var task = new GetReferenceNearestTargetFrameworkTask
            {
                BuildEngine = buildEngine,
                CurrentProjectTargetFramework = "netcoreapp1.0",
                FallbackTargetFrameworks = new string[] { "net45" },
                AnnotatedProjectReferences = references.ToArray()
            };

            var result = task.Execute();
            result.Should().BeFalse();

            task.AssignedProjects.Should().HaveCount(1);

            testLogger.Warnings.Should().Be(0);
            testLogger.Errors.Should().Be(1);
            testLogger.DebugMessages.Count.Should().Be(DEBUG_MESSAGE_COUNT_INPUT_OUTPUT);
        }
    }
}
