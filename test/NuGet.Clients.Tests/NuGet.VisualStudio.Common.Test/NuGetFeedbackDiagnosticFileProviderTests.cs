// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGet.PackageManagement;
using NuGet.ProjectManagement;
using Xunit;

namespace NuGet.VisualStudio.Common.Test
{
    public class NuGetFeedbackDiagnosticFileProviderTests
    {
        private NuGetFeedbackDiagnosticFileProvider _target;
        private Mock<ISolutionManager> _solutionManager;

        public NuGetFeedbackDiagnosticFileProviderTests()
        {
            _solutionManager = new Mock<ISolutionManager>();
            _solutionManager.Setup(sm => sm.GetNuGetProjectsAsync())
                .Returns(Task.FromResult<IEnumerable<NuGetProject>>(Array.Empty<NuGetProject>())); // empty or no solution

            _target = new NuGetFeedbackDiagnosticFileProvider();
            _target.SolutionManager = _solutionManager.Object;
        }

        [Fact]
        public async void GetFiles_NoSolutionMock_ReturnsZip()
        {
            // Arrange - also see constructor
            List<Task> backgroundTasks = new();
            _target.BackgroundTaskStarted += (_, task) => backgroundTasks.Add(task);

            // Act
            IReadOnlyCollection<string> files = _target.GetFiles();
            await Task.WhenAll(backgroundTasks);

            try
            {
                // Assert
                // As per feedback team's docs, multiple files should be saved in a zip, so our class should only
                // return that 1 filename.
                string fullPath = Assert.Single(files);
                Assert.EndsWith(".zip", fullPath);
                Assert.True(Path.IsPathRooted(fullPath));
                Assert.True(File.Exists(fullPath));

                // ensure file is readable (file handle isn't still open with FileShare.None)
                File.OpenRead(fullPath).Dispose();
            }
            finally
            {
                foreach (var file in files)
                {
                    File.Delete(file);
                }
            }
        }

        [Fact]
        public async Task WriteToZipAsync_NoSolutionMock_ContainsOnlyExpectedFiles()
        {
            // Arrange
            using var stream = new MemoryStream();

            // Act
            await _target.WriteToZipAsync(stream);

            // Assert
            using (var zip = new ZipArchive(stream))
            {
                var zipFiles = zip.Entries.Select(e => e.FullName);
                var expectedFiles = new[] { "dgspec.json" };

                Assert.Equal(zipFiles.OrderBy(f => f), expectedFiles);
            }
        }
    }
}
