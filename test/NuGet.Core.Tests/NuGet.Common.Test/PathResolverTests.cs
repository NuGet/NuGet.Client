// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Common.Test
{
    public class PathResolverTests : IClassFixture<TestDirectoryFixture>
    {
        private TestDirectoryFixture _fixture;

        public PathResolverTests(TestDirectoryFixture fixture)
        {
            _fixture = fixture;
        }

        [Theory]
        [InlineData("", false)]
        [InlineData("*", true)]
        [InlineData("a.*", true)]
        [InlineData("*.b", true)]
        [InlineData("*.*", true)]
        [InlineData("a/b", false)]
        [InlineData("a/b.*", true)]
        [InlineData(@"a\b", false)]
        [InlineData(@"a\b.*", true)]
        [InlineData("**", true)]
        [InlineData("**/a", true)]
        [InlineData("**/*.a", true)]
        [InlineData(@"**\a", true)]
        [InlineData(@"**\*.a", true)]
        [InlineData("?", false)]
        [InlineData("[", false)]
        public void PathResolver_IsWildcardSearch(string filter, bool expectedResult)
        {
            Assert.Equal(expectedResult, PathResolver.IsWildcardSearch(filter));
        }

        [Theory]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("/", false)]
        [InlineData(@"\", false)]
        [InlineData("a", false)]
        [InlineData("a/", true)]
        [InlineData("/a", false)]
        [InlineData("/a/", true)]
        public void PathResolver_IsDirectoryPath(string path, bool expectedResult)
        {
            Assert.Equal(expectedResult, PathResolver.IsDirectoryPath(path));
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData(@"a\", true)]
        [InlineData(@"\\a", false)]
        [InlineData(@"\\a\", true)]
        [InlineData(@"\\a\b", false)]
        [InlineData(@"\\a\c\", true)]
        [InlineData("C:", false)]
        [InlineData(@"C:\a", false)]
        [InlineData(@"C:\a\", true)]
        [InlineData(@".\", true)]
        [InlineData(@"..\", true)]
        public void PathResolver_IsDirectoryPath_OnWindows(string path, bool expectedResult)
        {
            Assert.Equal(expectedResult, PathResolver.IsDirectoryPath(path));
        }

        [PlatformTheory(Platform.Darwin)]
        [InlineData(@"a\", false)]
        [InlineData(@"\\a", false)]
        [InlineData(@"\\a\", false)]
        [InlineData(@"\\a\b", false)]
        [InlineData(@"\\a\c\", false)]
        [InlineData("C:", false)]
        [InlineData(@"C:\a", false)]
        [InlineData(@"C:\a\", false)]
        [InlineData(@".\", false)]
        [InlineData(@"..\", false)]
        public void PathResolver_IsDirectoryPath_OnMacOS(string path, bool expectedResult)
        {
            Assert.Equal(expectedResult, PathResolver.IsDirectoryPath(path));
        }

        [PlatformTheory(Platform.Linux)]
        [InlineData(@"a\", false)]
        [InlineData(@"\\a", false)]
        [InlineData(@"\\a\", false)]
        [InlineData(@"\\a\b", false)]
        [InlineData(@"\\a\c\", false)]
        [InlineData("C:", false)]
        [InlineData(@"C:\a", false)]
        [InlineData(@"C:\a\", false)]
        [InlineData(@".\", false)]
        [InlineData(@"..\", false)]
        public void PathResolver_IsDirectoryPath_OnLinux(string path, bool expectedResult)
        {
            Assert.Equal(expectedResult, PathResolver.IsDirectoryPath(path));
        }

        [Theory]
        [InlineData("**")]
        [InlineData("**/")]
        [InlineData(@"**\")]
        [InlineData("**/a/*.b")]
        [InlineData(@"**\a\*.b")]
        public void PathResolver_NormalizeWildcardForExcludedFiles_WithLeadingGlobstarReturnsWildcardAsIs(string wildcard)
        {
            var actualResult = PathResolver.NormalizeWildcardForExcludedFiles(_fixture.Path, wildcard);

            Assert.Equal(wildcard, actualResult);
        }

        [Fact]
        public void PathResolver_NormalizeWildcardForExcludedFiles_HandlesLeadingOsSpecificParentPathReference()
        {
            var basePath = Path.Combine(_fixture.Path, "dir1", "dir2");
            var wildcard = string.Format("..{0}..{0}*", Path.DirectorySeparatorChar);
            var expectedResult = string.Format("{0}{1}*", new DirectoryInfo(_fixture.Path).FullName, Path.DirectorySeparatorChar);
            var actualResult = PathResolver.NormalizeWildcardForExcludedFiles(basePath, wildcard);

            Assert.Equal(expectedResult, actualResult);
        }

        [Fact]
        public void PathResolver_GetMatches_HandlesDotGlobstar()
        {
            var sources = GetPlatformSpecificPaths(new[] { ".{0}a{0}.c", ".{0}a{0}c", ".{0}.c", ".{0}c", "bc", "b.c", "c", ".c" });
            var wildcards = new[] { ".**" };
            var actualResults = PathResolver.GetMatches(sources, source => source, wildcards);
            var expectedResults = GetPlatformSpecificPaths(new[] { ".c" });

            Assert.Equal(expectedResults, actualResults);
        }

        [Fact]
        public void PathResolver_GetMatches_HandlesGlobstarSlash()
        {
            var sources = GetPlatformSpecificPaths(new[] { "a{0}d", "a{0}de", "a{0}b{0}d", "a{0}b{0}de", "a{0}b{0}c{0}d", "a{0}b{0}c{0}de" });
            var wildcards = GetPlatformSpecificPaths(new[] { "a{0}**{0}d" });
            var actualResults = PathResolver.GetMatches(sources, source => source, wildcards);
            var expectedResults = GetPlatformSpecificPaths(new[] { "a{0}d", "a{0}b{0}d", "a{0}b{0}c{0}d" });

            Assert.Equal(expectedResults, actualResults);
        }

        [Fact]
        public void PathResolver_GetMatches_WithGlobstarSlashFileNameMatchesFileName()
        {
            var sources = GetPlatformSpecificPaths(new[] { ".{0}c", "a{0}c", "a{0}bc", "bc" });
            var wildcards = GetPlatformSpecificPaths(new[] { "**{0}c" });
            var actualResults = PathResolver.GetMatches(sources, source => source, wildcards);
            var expectedResults = GetPlatformSpecificPaths(new[] { ".{0}c", "a{0}c" });

            Assert.Equal(expectedResults, actualResults);
        }

        [Fact]
        public void PathResolver_GetMatches_HandlesGlobstar()
        {
            var sources = GetPlatformSpecificPaths(new[] { "a{0}d", "a{0}de", "a{0}b{0}d", "a{0}b{0}de", "a{0}b{0}c{0}d", "a{0}b{0}c{0}de" });
            var wildcards = GetPlatformSpecificPaths(new[] { "a{0}**d" });
            var actualResults = PathResolver.GetMatches(sources, source => source, wildcards);
            var expectedResults = GetPlatformSpecificPaths(new[] { "a{0}d", "a{0}b{0}d", "a{0}b{0}c{0}d" });

            Assert.Equal(expectedResults, actualResults);
        }

        [Fact]
        public void PathResolver_GetMatchesHandlesStar()
        {
            var sources = GetPlatformSpecificPaths(new[] { "a{0}d", "a{0}de", "a{0}b{0}d", "a{0}b{0}de", "a{0}b{0}c{0}d", "a{0}b{0}c{0}de" });
            var wildcards = GetPlatformSpecificPaths(new[] { "a{0}*{0}*{0}d" });
            var actualResults = PathResolver.GetMatches(sources, source => source, wildcards);
            var expectedResults = GetPlatformSpecificPaths(new[] { "a{0}b{0}c{0}d" });

            Assert.Equal(expectedResults, actualResults);
        }

        [Fact]
        public void PathResolver_GetMatches_HandlesQuestionMark()
        {
            var sources = new[] { "a", "ab", "abc", "ac", "adc" };
            var wildcards = new[] { "a?c" };
            var actualResults = PathResolver.GetMatches(sources, source => source, wildcards);
            var expectedResults = new[] { "abc", "adc" };

            Assert.Equal(expectedResults, actualResults);
        }

        [Fact]
        public void PathResolver_FilterPackageFiles_HandlesWildcard()
        {
            var sources = new List<string>(GetPlatformSpecificPaths(new[] { "c", "a{0}c", "a{0}b{0}c", "a{0}d" }));
            var wildcards = GetPlatformSpecificPaths(new[] { "**{0}c" });

            PathResolver.FilterPackageFiles(sources, path => path, wildcards);

            var expectedResults = GetPlatformSpecificPaths(new[] { "a{0}d" });
            Assert.Equal(expectedResults, sources);
        }

        [Theory]
        [InlineData("dir1/dir2")]
        [InlineData(@"dir1\dir2")]
        public void PathResolver_PerformWildcardSearch_WithDirectoryFindsNoMatchingFiles(string searchPath)
        {
            var actualFullPaths = PathResolver.PerformWildcardSearch(_fixture.Path, searchPath);

            Verify(new string[] { }, actualFullPaths);
        }

        [Fact]
        public void PathResolver_PerformWildcardSearch_WithDirectoryFindsMatchingEmptyDirectory()
        {
            string normalizedBasePath;
            var actualResults = PathResolver.PerformWildcardSearch(_fixture.Path, "dir*", includeEmptyDirectories: true, normalizedBasePath: out normalizedBasePath);

            Assert.Collection(actualResults, result =>
            {
                Assert.False(result.IsFile);

                var expectedRelativePath = string.Format("{0}dir5", Path.DirectorySeparatorChar);
                var actualRelativePath = result.Path.Substring(_fixture.Path.Length);

                Assert.Equal(expectedRelativePath, actualRelativePath);
            });
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("dir1/dir2/")]
        [InlineData(@"dir1\dir2\")]
        public void PathResolver_PerformWildcardSearch_WithDirectoryAndTrailingSlashRecursivelyFindsAllMatchingFiles_OnWindows(string searchPath)
        {
            var actualFullPaths = PathResolver.PerformWildcardSearch(_fixture.Path, searchPath);

            Verify(new[]
                {
                    @"\dir1\dir2\file1.txt",
                    @"\dir1\dir2\file2.txt",
                    @"\dir1\dir2\dir3\file1.txt",
                    @"\dir1\dir2\dir3\file2.txt"
                }, actualFullPaths);
        }

        [PlatformTheory(Platform.Darwin)]
        [InlineData("dir1/dir2/", new[]
            {
                "/dir1/dir2/file1.txt",
                "/dir1/dir2/file2.txt",
                "/dir1/dir2/dir3/file1.txt",
                "/dir1/dir2/dir3/file2.txt"
            })]
        [InlineData(@"dir1\dir2\", new string[] { })]
        public void PathResolver_PerformWildcardSearch_WithDirectoryAndTrailingSlashRecursivelyFindsAllMatchingFiles_OnMacOs(string searchPath, string[] expectedResults)
        {
            var actualFullPaths = PathResolver.PerformWildcardSearch(_fixture.Path, searchPath);

            Verify(expectedResults, actualFullPaths);
        }

        [PlatformTheory(Platform.Linux)]
        [InlineData("dir1/dir2/", new[]
            {
                "/dir1/dir2/file1.txt",
                "/dir1/dir2/file2.txt",
                "/dir1/dir2/dir3/file1.txt",
                "/dir1/dir2/dir3/file2.txt"
            })]
        [InlineData(@"dir1\dir2\", new string[] { })]
        public void PathResolver_PerformWildcardSearch_WithDirectoryAndTrailingSlashRecursivelyFindsAllMatchingFiles_OnLinux(string searchPath, string[] expectedResults)
        {
            var actualFullPaths = PathResolver.PerformWildcardSearch(_fixture.Path, searchPath);

            Verify(expectedResults, actualFullPaths);
        }

        [Fact]
        public void PathResolver_PerformWildcardSearch_WithFileNameFindsNoMatchingFile()
        {
            var actualFullPaths = PathResolver.PerformWildcardSearch(_fixture.Path, "file3.txt");

            Verify(new string[] { }, actualFullPaths);
        }

        [Fact]
        public void PathResolver_PerformWildcardSearch_WithFileNameFindsMatchingFile()
        {
            var actualFullPaths = PathResolver.PerformWildcardSearch(_fixture.Path, "file1.txt");

            Verify(new[]
                {
                    $"{Path.DirectorySeparatorChar}file1.txt"
                }, actualFullPaths);
        }

        [Fact]
        public void PathResolver_PerformWildcardSearch_WithFileNamePatternFindsMatchingFiles()
        {
            var actualFullPaths = PathResolver.PerformWildcardSearch(_fixture.Path, "*.txt");

            Verify(new[]
                {
                    $"{Path.DirectorySeparatorChar}file1.txt",
                    $"{Path.DirectorySeparatorChar}file2.txt"
                }, actualFullPaths);
        }

        [Fact]
        public void PathResolver_PerformWildcardSearch_WithFileNamePatternFindsNoMatchingFile()
        {
            var actualFullPaths = PathResolver.PerformWildcardSearch(_fixture.Path, "*.dll");

            Verify(new string[] { }, actualFullPaths);
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("dir1/file2.txt")]
        [InlineData(@"dir1\file2.txt")]
        public void PathResolver_PerformWildcardSearch_WithDirectoryAndFileNameFindsMatchingFile_OnWindows(string searchPath)
        {
            var actualFullPaths = PathResolver.PerformWildcardSearch(_fixture.Path, searchPath);

            Verify(new[] { @"\dir1\file2.txt" }, actualFullPaths);
        }

        [PlatformTheory(Platform.Darwin)]
        [InlineData("dir1/file2.txt", new[] { "/dir1/file2.txt" })]
        [InlineData(@"dir1\file2.txt", new string[] { })]
        public void PathResolver_PerformWildcardSearch_WithDirectoryAndFileNameFindsMatchingFile_OnMacOs(string searchPath, string[] expectedResults)
        {
            var actualFullPaths = PathResolver.PerformWildcardSearch(_fixture.Path, searchPath);

            Verify(expectedResults, actualFullPaths);
        }

        [PlatformTheory(Platform.Linux)]
        [InlineData("dir1/file2.txt", new[] { "/dir1/file2.txt" })]
        [InlineData(@"dir1\file2.txt", new string[] { })]
        public void PathResolver_PerformWildcardSearch_WithDirectoryAndFileNameFindsMatchingFile_OnLinux(string searchPath, string[] expectedResults)
        {
            var actualFullPaths = PathResolver.PerformWildcardSearch(_fixture.Path, searchPath);

            Verify(expectedResults, actualFullPaths);
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("dir1/../file1.txt")]
        [InlineData(@"dir1\..\file1.txt")]
        public void PathResolver_PerformWildcardSearch_WithDirectoryRelativePathAndFileNameFindsMatchingFile_OnWindows(string searchPath)
        {
            var actualFullPaths = PathResolver.PerformWildcardSearch(_fixture.Path, searchPath);

            Verify(new[] { @"\dir1\..\file1.txt" }, actualFullPaths);
        }

        [PlatformTheory(Platform.Darwin)]
        [InlineData("dir1/../file1.txt", new[] { "/dir1/../file1.txt" })]
        [InlineData(@"dir1\..\file1.txt", new string[] { })]
        public void PathResolver_PerformWildcardSearch_WithDirectoryRelativePathAndFileNameFindsMatchingFile_OnMacOs(string searchPath, string[] expectedResults)
        {
            var actualFullPaths = PathResolver.PerformWildcardSearch(_fixture.Path, searchPath);

            Verify(expectedResults, actualFullPaths);
        }

        [PlatformTheory(Platform.Linux)]
        [InlineData("dir1/../file1.txt", new[] { "/dir1/../file1.txt" })]
        [InlineData(@"dir1\..\file1.txt", new string[] { })]
        public void PathResolver_PerformWildcardSearch_WithDirectoryRelativePathAndFileNameFindsMatchingFile_OnLinux(string searchPath, string[] expectedResults)
        {
            var actualFullPaths = PathResolver.PerformWildcardSearch(_fixture.Path, searchPath);

            Verify(expectedResults, actualFullPaths);
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("**/file1.txt")]
        [InlineData(@"**\file1.txt")]
        public void PathResolver_PerformWildcardSearch_WithGlobstarAndFileNameRecursivelyFindsAllMatchingFiles(string searchPath)
        {
            var actualFullPaths = PathResolver.PerformWildcardSearch(_fixture.Path, searchPath);

            Verify(new[]
                {
                    @"\file1.txt",
                    @"\dir1\file1.txt",
                    @"\dir1\dir2\file1.txt",
                    @"\dir1\dir2\dir3\file1.txt",
                    @"\dir1\dir4\file1.txt"
                }, actualFullPaths);
        }

        [PlatformTheory(Platform.Darwin)]
        [InlineData("**/file1.txt", new[]
            {
                "/file1.txt",
                "/dir1/file1.txt",
                "/dir1/dir2/file1.txt",
                "/dir1/dir2/dir3/file1.txt",
                "/dir1/dir4/file1.txt"
            })]
        [InlineData(@"**\file1.txt", new string[] { })]
        public void PathResolver_PerformWildcardSearch_WithGlobstarAndFileNameRecursivelyFindsAllMatchingFiles_OnMacOs(string searchPath, string[] expectedResults)
        {
            var actualFullPaths = PathResolver.PerformWildcardSearch(_fixture.Path, searchPath);

            Verify(expectedResults, actualFullPaths);
        }

        [PlatformTheory(Platform.Linux)]
        [InlineData("**/file1.txt", new[]
            {
                "/file1.txt",
                "/dir1/file1.txt",
                "/dir1/dir2/file1.txt",
                "/dir1/dir2/dir3/file1.txt",
                "/dir1/dir4/file1.txt"
            })]
        [InlineData(@"**\file1.txt", new string[] { })]
        public void PathResolver_PerformWildcardSearch_WithGlobstarAndFileNameRecursivelyFindsAllMatchingFiles_OnLinux(string searchPath, string[] expectedResults)
        {
            var actualFullPaths = PathResolver.PerformWildcardSearch(_fixture.Path, searchPath);

            Verify(expectedResults, actualFullPaths);
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("**/*.txt")]
        [InlineData(@"**\*.txt")]
        public void PathResolver_PerformWildcardSearch_WithGlobstarAndFileNamePatternRecursivelyFindsAllMatchingFiles_OnWindows(string searchPath)
        {
            var actualFullPaths = PathResolver.PerformWildcardSearch(_fixture.Path, searchPath);

            Verify(new[]
                {
                    @"\file1.txt",
                    @"\file2.txt",
                    @"\dir1\file1.txt",
                    @"\dir1\file2.txt",
                    @"\dir1\dir2\file1.txt",
                    @"\dir1\dir2\file2.txt",
                    @"\dir1\dir2\dir3\file1.txt",
                    @"\dir1\dir2\dir3\file2.txt",
                    @"\dir1\dir4\file1.txt",
                    @"\dir1\dir4\file2.txt"
                }, actualFullPaths);
        }

        [PlatformTheory(Platform.Darwin)]
        [InlineData("**/*.txt", new[]
            {
                "/file1.txt",
                "/file2.txt",
                "/dir1/file1.txt",
                "/dir1/file2.txt",
                "/dir1/dir2/file1.txt",
                "/dir1/dir2/file2.txt",
                "/dir1/dir2/dir3/file1.txt",
                "/dir1/dir2/dir3/file2.txt",
                "/dir1/dir4/file1.txt",
                "/dir1/dir4/file2.txt"
            })]
        [InlineData(@"**\*.txt", new string[] { })]
        public void PathResolver_PerformWildcardSearch_WithGlobstarAndFileNamePatternRecursivelyFindsAllMatchingFiles_OnMacOs(string searchPath, string[] expectedResults)
        {
            var actualFullPaths = PathResolver.PerformWildcardSearch(_fixture.Path, searchPath);

            Verify(expectedResults, actualFullPaths);
        }

        [PlatformTheory(Platform.Linux)]
        [InlineData("**/*.txt", new[]
            {
                "/file1.txt",
                "/file2.txt",
                "/dir1/file1.txt",
                "/dir1/file2.txt",
                "/dir1/dir2/file1.txt",
                "/dir1/dir2/file2.txt",
                "/dir1/dir2/dir3/file1.txt",
                "/dir1/dir2/dir3/file2.txt",
                "/dir1/dir4/file1.txt",
                "/dir1/dir4/file2.txt"
            })]
        [InlineData(@"**\*.txt", new string[] { })]
        public void PathResolver_PerformWildcardSearch_WithGlobstarAndFileNamePatternRecursivelyFindsAllMatchingFiles_OnLinux(string searchPath, string[] expectedResults)
        {
            var actualFullPaths = PathResolver.PerformWildcardSearch(_fixture.Path, searchPath);

            Verify(expectedResults, actualFullPaths);
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("dir1/**/file2.txt")]
        [InlineData(@"dir1\**\file2.txt")]
        public void PathResolver_PerformWildcardSearch_WithDirectoryGlobstarAndFileNameAtNonRootDirectoryRecursivelyFindsAllMatchingFiles_OnWindows(string searchPath)
        {
            var actualFullPaths = PathResolver.PerformWildcardSearch(_fixture.Path, searchPath);

            Verify(new[]
                {
                    @"\dir1\file2.txt",
                    @"\dir1\dir2\file2.txt",
                    @"\dir1\dir2\dir3\file2.txt",
                    @"\dir1\dir4\file2.txt"
                }, actualFullPaths);
        }

        [PlatformTheory(Platform.Darwin)]
        [InlineData("dir1/**/file2.txt", new[]
            {
                "/dir1/file2.txt",
                "/dir1/dir2/file2.txt",
                "/dir1/dir2/dir3/file2.txt",
                "/dir1/dir4/file2.txt"
            })]
        [InlineData(@"dir1\**\file2.txt", new string[] { })]
        public void PathResolver_PerformWildcardSearch_WithDirectoryGlobstarAndFileNameAtNonRootDirectoryRecursivelyFindsAllMatchingFiles_OnMacOs(string searchPath, string[] expectedResults)
        {
            var actualFullPaths = PathResolver.PerformWildcardSearch(_fixture.Path, searchPath);

            Verify(expectedResults, actualFullPaths);
        }

        [PlatformTheory(Platform.Linux)]
        [InlineData("dir1/**/file2.txt", new[]
            {
                "/dir1/file2.txt",
                "/dir1/dir2/file2.txt",
                "/dir1/dir2/dir3/file2.txt",
                "/dir1/dir4/file2.txt"
            })]
        [InlineData(@"dir1\**\file2.txt", new string[] { })]
        public void PathResolver_PerformWildcardSearch_WithDirectoryGlobstarAndFileNameAtNonRootDirectoryRecursivelyFindsAllMatchingFiles_OnLinux(string searchPath, string[] expectedResults)
        {
            var actualFullPaths = PathResolver.PerformWildcardSearch(_fixture.Path, searchPath);

            Verify(expectedResults, actualFullPaths);
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("dir1/dir*/*.txt")]
        [InlineData(@"dir1\dir*\*.txt")]
        public void PathResolver_PerformWildcardSearch_WithDirectoryPatternAndFileNamePatternRecursivelyFindsAllMatchingFiles_OnWindows(string searchPath)
        {
            var actualFullPaths = PathResolver.PerformWildcardSearch(_fixture.Path, searchPath);

            Verify(new[]
                {
                    @"\dir1\dir2\file1.txt",
                    @"\dir1\dir2\file2.txt",
                    @"\dir1\dir4\file1.txt",
                    @"\dir1\dir4\file2.txt"
                }, actualFullPaths);
        }

        [PlatformTheory(Platform.Darwin)]
        [InlineData("dir1/dir*/*.txt", new[]
            {
                "/dir1/dir2/file1.txt",
                "/dir1/dir2/file2.txt",
                "/dir1/dir4/file1.txt",
                "/dir1/dir4/file2.txt"
            })]
        [InlineData(@"dir1\dir*\*.txt", new string[] { })]
        public void PathResolver_PerformWildcardSearch_WithDirectoryPatternAndFileNamePatternRecursivelyFindsAllMatchingFiles_OnMacOs(string searchPath, string[] expectedResults)
        {
            var actualFullPaths = PathResolver.PerformWildcardSearch(_fixture.Path, searchPath);

            Verify(expectedResults, actualFullPaths);
        }

        [PlatformTheory(Platform.Linux)]
        [InlineData("dir1/dir*/*.txt", new[]
            {
                "/dir1/dir2/file1.txt",
                "/dir1/dir2/file2.txt",
                "/dir1/dir4/file1.txt",
                "/dir1/dir4/file2.txt"
            })]
        [InlineData(@"dir1\dir*\*.txt", new string[] { })]
        public void PathResolver_PerformWildcardSearch_WithDirectoryPatternAndFileNamePatternRecursivelyFindsAllMatchingFiles_OnLinux(string searchPath, string[] expectedResults)
        {
            var actualFullPaths = PathResolver.PerformWildcardSearch(_fixture.Path, searchPath);

            Verify(expectedResults, actualFullPaths);
        }

        private void Verify(IEnumerable<string> expectedRelativePaths, IEnumerable<string> actualFullPaths)
        {
            var actualRelativePaths = actualFullPaths.Select(fullPath => fullPath.Substring(_fixture.Path.Length));
            var expectedResults = expectedRelativePaths.OrderBy(path => path);
            var actualResults = actualRelativePaths.OrderBy(path => path);

            Assert.Equal(expectedResults, actualResults);
        }

        private static IEnumerable<string> GetPlatformSpecificPaths(IEnumerable<string> platformUnspecificPaths)
        {
            return platformUnspecificPaths.Select(path => string.Format(path, Path.DirectorySeparatorChar));
        }
    }

    /*
    Test directory contents:

        dir1
            dir2
                dir3
                    file1.txt
                    file2.txt
                file1.txt
                file2.txt
            dir4
                file1.txt
                file2.txt
            file1.txt
            file2.txt
        dir5
        file1.txt
        file2.txt
    */
    public sealed class TestDirectoryFixture : IDisposable
    {
        private TestDirectory _rootDirectory;

        public string Path
        {
            get { return _rootDirectory.Path; }
        }

        public TestDirectoryFixture()
        {
            _rootDirectory = TestDirectory.Create();

            PopulateTestDirectory();
        }

        public void Dispose()
        {
            _rootDirectory.Dispose();
        }

        private void PopulateTestDirectory()
        {
            var rootDirectory = new DirectoryInfo(_rootDirectory.Path);
            var directory1 = Directory.CreateDirectory(System.IO.Path.Combine(rootDirectory.FullName, "dir1"));
            var directory2 = Directory.CreateDirectory(System.IO.Path.Combine(directory1.FullName, "dir2"));
            var directory3 = Directory.CreateDirectory(System.IO.Path.Combine(directory2.FullName, "dir3"));
            var directory4 = Directory.CreateDirectory(System.IO.Path.Combine(directory1.FullName, "dir4"));
            var directory5 = Directory.CreateDirectory(System.IO.Path.Combine(rootDirectory.FullName, "dir5"));

            CreateTestFiles(rootDirectory);
            CreateTestFiles(directory1);
            CreateTestFiles(directory2);
            CreateTestFiles(directory3);
            CreateTestFiles(directory4);
        }

        private static void CreateTestFiles(DirectoryInfo directory)
        {
            File.WriteAllText(System.IO.Path.Combine(directory.FullName, "file1.txt"), string.Empty);
            File.WriteAllText(System.IO.Path.Combine(directory.FullName, "file2.txt"), string.Empty);
        }
    }
}