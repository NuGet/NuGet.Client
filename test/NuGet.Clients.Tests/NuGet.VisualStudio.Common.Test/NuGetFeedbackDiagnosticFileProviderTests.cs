// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Sdk.TestFramework;
using Moq;
using NuGet.PackageManagement;
using NuGet.ProjectManagement;
using Xunit;

namespace NuGet.VisualStudio.Common.Test
{
    [Collection(MockedVS.Collection)]
    public class NuGetFeedbackDiagnosticFileProviderTests
    {
        private NuGetFeedbackDiagnosticFileProvider _target;
        private Mock<ISolutionManager> _solutionManager;

        public NuGetFeedbackDiagnosticFileProviderTests(GlobalServiceProvider globalServiceProvider)
        {
            // NuGetFeedbackGiagnosticFileProvider uses ThreadHelper.JoinableTaskFactory, so MockedVS is needed
            globalServiceProvider.Reset();

            _solutionManager = new Mock<ISolutionManager>();
            _solutionManager.Setup(sm => sm.GetNuGetProjectsAsync())
                .Returns(Task.FromResult<IEnumerable<NuGetProject>>(Array.Empty<NuGetProject>())); // empty or no solution

            _target = new NuGetFeedbackDiagnosticFileProvider();
            _target.SolutionManager = _solutionManager.Object;
        }

        [Fact]
        public void EnsureSingleFileWithFullPathIsReturned()
        {
            // Arranged in constructor
            // Act
            IReadOnlyCollection<string> files = _target.GetFiles();

            try
            {
                // Assert
                // As per feedback team's docs, multiple files should be saved in a zip, so our class should only
                // return that 1 filename.I guess the interface // returning a collection was a design mistake that
                // can't be broken for backwards compat reasons.
                var file = Assert.Single(files);
                Assert.EndsWith(".zip", file);
                Assert.True(Path.IsPathRooted(file));
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
        public void EnsureContainsOnlyExpectedFiles()
        {
            // Arranged in constructor
            // Act
            IReadOnlyCollection<string> files = _target.GetFiles();

            try
            {
                // Assert
                var file = Assert.Single(files);
                using (var fileStream = File.OpenRead(file))
                using (var zip = new ZipArchive(fileStream))
                {
                    var zipFiles = zip.Entries.Select(e => e.FullName);
                    var expectedFiles = new[] { "dgspec.json" };

                    Assert.Equal(zipFiles.OrderBy(f => f), expectedFiles);
                }
            }
            finally
            {
                foreach (var file in files)
                {
                    File.Delete(file);
                }
            }
        }
    }
}
