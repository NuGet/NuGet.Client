// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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
        //[Fact]
        //public async void LoadReadme_NullPackage_Error()
        //{
        //    //Arrange
        //    var readMePreviewViewModel = new ReadMePreviewViewModel();

        //    //Act
        //    await Assert.ThrowsAnyAsync<Exception>(async () => await readMePreviewViewModel.LoadReadme(null));

        //    //Assert
        //    Assert.False(readMePreviewViewModel.IsErrorWithReadMe);
        //    Assert.Equal(string.Empty, readMePreviewViewModel.ReadMeMarkdown);
        //    Assert.True(readMePreviewViewModel.CanDetermineReadMeDefined);
        //}

        //[Fact]
        //public async void LoadReadme_PackageWithNoReadme_NoErrorNoReadmeMarkdown()
        //{
        //    //Arrange
        //    var readMePreviewViewModel = new ReadMePreviewViewModel();
        //    Mock<DetailedPackageMetadata> packageMetadata = new Mock<DetailedPackageMetadata>();
        //    bool? falseValue = false;
        //    packageMetadata.Setup(x => x.TryGetReadme()).Returns(Task.FromResult((falseValue, string.Empty)));

        //    //Act
        //    await readMePreviewViewModel.LoadReadme(packageMetadata.Object);

        //    //Assert
        //    Assert.False(readMePreviewViewModel.IsErrorWithReadMe);
        //    Assert.Equal(string.Empty, readMePreviewViewModel.ReadMeMarkdown);
        //    Assert.True(readMePreviewViewModel.CanDetermineReadMeDefined);
        //}

        //[Fact]
        //public async void LoadReadme_PackageWithReadme_NoErrorNoReadmeMarkdown()
        //{
        //    //Arrange
        //    var readMePreviewViewModel = new ReadMePreviewViewModel();
        //    Mock<DetailedPackageMetadata> packageMetadata = new Mock<DetailedPackageMetadata>();
        //    bool? trueValue = true;
        //    var readmeContent = "some readme content";
        //    packageMetadata.Setup(x => x.TryGetReadme()).Returns(Task.FromResult((trueValue, readmeContent)));

        //    //Act
        //    await readMePreviewViewModel.LoadReadme(packageMetadata.Object);

        //    //Assert
        //    Assert.False(readMePreviewViewModel.IsErrorWithReadMe);
        //    Assert.Equal(readmeContent, readMePreviewViewModel.ReadMeMarkdown);
        //    Assert.True(readMePreviewViewModel.CanDetermineReadMeDefined);
        //}

        //[Fact]
        //public async void LoadReadme_CannotDetermineReadme_NoErrorNoReadmeMarkdown()
        //{
        //    //Arrange
        //    var readMePreviewViewModel = new ReadMePreviewViewModel();
        //    Mock<DetailedPackageMetadata> packageMetadata = new Mock<DetailedPackageMetadata>();
        //    bool? nullValue = null;
        //    packageMetadata.Setup(x => x.TryGetReadme()).Returns(Task.FromResult((nullValue,string.Empty)));

        //    //Act
        //    await readMePreviewViewModel.LoadReadme(packageMetadata.Object);

        //    //Assert
        //    Assert.False(readMePreviewViewModel.IsErrorWithReadMe);
        //    Assert.Equal(string.Empty, readMePreviewViewModel.ReadMeMarkdown);
        //    Assert.False(readMePreviewViewModel.CanDetermineReadMeDefined);
        //}
    }
}
