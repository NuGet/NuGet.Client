// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using Xunit;

namespace NuGet.VisualStudio.Common.Test
{
    public class NuGetFeedbackDiagnosticFileProviderTests
    {
        [Fact]
        public void GetFilesTest()
        {
            // Arrange
            var provider = new NuGetFeedbackDiagnosticFileProvider();

            // Act
            var files = provider.GetFiles();

            // Assert
            foreach (var file in files)
            {
                Assert.True(Path.IsPathRooted(file));
            }
        }
    }
}
