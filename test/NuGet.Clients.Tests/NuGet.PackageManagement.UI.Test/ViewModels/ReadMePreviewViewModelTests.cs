// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.VisualStudio.Sdk.TestFramework;
using Moq;
using NuGet.PackageManagement.UI.ViewModels;
using Xunit;

namespace NuGet.PackageManagement.UI.Test.ViewModels
{
    [Collection(MockedVS.Collection)]
    public class ReadMePreviewViewModelTests
    {
        [Fact]
        public async void ReadMePreviewViewModelTests_PackageWithNoReadme_NoErrorNoReadmeMarkdown()
        {
            var readMePreviewViewModel = new ReadMePreviewViewModel();

            Mock<DetailedPackageMetadata> packageMetadata = new Mock<DetailedPackageMetadata>();
            bool? falseValue = false;
            packageMetadata.Setup(x => x.GetHasReadMe()).Returns(Task.FromResult(falseValue));

            await readMePreviewViewModel.LoadReadme(packageMetadata.Object);

            Assert.False(readMePreviewViewModel.IsErrorWithReadMe);
            Assert.Equal(string.Empty, readMePreviewViewModel.ReadMeMarkdown);
            Assert.True(readMePreviewViewModel.CanDetermineReadMeDefined);
        }

        [Fact]
        public async void ReadMePreviewViewModelTests_PackageWithReadme_NoErrorNoReadmeMarkdown()
        {
            var readMePreviewViewModel = new ReadMePreviewViewModel();

            Mock<DetailedPackageMetadata> packageMetadata = new Mock<DetailedPackageMetadata>();
            bool? trueValue = true;
            var readmeContent = "some readme content";
            packageMetadata.Setup(x => x.GetHasReadMe()).Returns(Task.FromResult(trueValue));
            packageMetadata.Setup(x => x.GetReadMe()).Returns(Task.FromResult(readmeContent));


            await readMePreviewViewModel.LoadReadme(packageMetadata.Object);

            Assert.False(readMePreviewViewModel.IsErrorWithReadMe);
            Assert.Equal(readmeContent, readMePreviewViewModel.ReadMeMarkdown);
            Assert.True(readMePreviewViewModel.CanDetermineReadMeDefined);
        }

        [Fact]
        public async void ReadMePreviewViewModelTests_CannotDetermineReadme_NoErrorNoReadmeMarkdown()
        {
            var readMePreviewViewModel = new ReadMePreviewViewModel();

            Mock<DetailedPackageMetadata> packageMetadata = new Mock<DetailedPackageMetadata>();
            bool? nullValue = null;
            packageMetadata.Setup(x => x.GetHasReadMe()).Returns(Task.FromResult(nullValue));


            await readMePreviewViewModel.LoadReadme(packageMetadata.Object);

            Assert.False(readMePreviewViewModel.IsErrorWithReadMe);
            Assert.Equal(string.Empty, readMePreviewViewModel.ReadMeMarkdown);
            Assert.False(readMePreviewViewModel.CanDetermineReadMeDefined);
        }
    }
}
