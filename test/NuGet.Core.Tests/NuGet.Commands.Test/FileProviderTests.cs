// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Xunit;

namespace NuGet.Commands.Test;

public sealed class FileProviderTests
{
    // These tests ensure behavioral parity between SingleFileProvider and VirtualFileProvider (with a single file)
    [Theory]
    [InlineData("a/b/file.txt", "ROOT", "a", true)]
    [InlineData("a/b/file.txt", "ROOT/a", "a/b", true)]
    [InlineData("a/b/file.txt", "ROOT/a/b", "a/b/file.txt", false)]
    [InlineData("a/b/file.txt", "a", "a/b", true)]
    [InlineData("a/b/file.txt", "a/b", "a/b/file.txt", false)]
    public void SingleSourceFile(string source, string search, string expected, bool isDirectory)
    {
        var contents = new VirtualFileProvider(new List<string> { source }).GetDirectoryContents(search);

        var content = Assert.Single(contents);

        Assert.Equal(expected, content.PhysicalPath);
        Assert.Equal(isDirectory, content.IsDirectory);

        contents = new SingleFileProvider(source).GetDirectoryContents(search);

        content = Assert.Single(contents);

        Assert.Equal(expected, content.PhysicalPath);
        Assert.Equal(isDirectory, content.IsDirectory);
    }

    [Theory]
    [InlineData("a/b/file.txt", "")]
    [InlineData("a/b/file.txt", "ROOT/z")]
    [InlineData("a/b/file.txt", "ROOT/aa")]
    [InlineData("a/b/file.txt", "ROOT/z/a")]
    [InlineData("a/b/file.txt", "ROOT/a/z")]
    [InlineData("a/b/file.txt", "ROOT/a/b/file")]
    [InlineData("a/b/file.txt", "ROOT/a/b/file.txt")]
    public void SingleSourceFile_NoMatch(string source, string search)
    {
        var contents = new VirtualFileProvider(new List<string> { source }).GetDirectoryContents(search);

        Assert.Empty(contents);

        contents = new SingleFileProvider(source).GetDirectoryContents(search);

        Assert.Empty(contents);
    }
}
