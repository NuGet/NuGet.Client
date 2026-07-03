// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Common.Test
{
    public class UriUtilityTests
    {
        [Theory]
        [InlineData("file:///test", "test")]
        [InlineData("file://test", "test")]
        [InlineData("https://api.nuget.org/v3/index.json", "https://api.nuget.org/v3/index.json")]
        [InlineData("a/b/c", "a/b/c")]
        [InlineData("", "")]
        [InlineData("ftp://test", "ftp://test")]
        [InlineData("a", "a")]
        [InlineData("..\\a", "..\\a")]
        public void UriUtility_GetLocalPath(string input, string expected)
        {
            // Arrange & Act
            var local = UriUtility.GetLocalPath(input);

            // Assert
            // Trim for xplat
            Assert.Equal(expected, local.TrimStart('\\').TrimStart('/'));
        }

        [Theory]
        [InlineData("test", "test")]
        [InlineData("test/../test2", "test2")]
        [InlineData("../test", "../test")]
        [InlineData("a/b/c", "a/b/c")]
        public void UriUtility_GetAbsolutePath_VerifyRelativePathCombined(string source, string relative)
        {
            using (var root = TestDirectory.Create())
            {
                var expected = Path.GetFullPath(Path.Combine(root, relative));

                var path = UriUtility.GetAbsolutePath(root, source);

                Assert.Equal(expected, path);
            }
        }

        [Fact]
        public void UriUtility_GetAbsolutePath_VerifyUrlPathUnchanged()
        {
            using (var root = TestDirectory.Create())
            {
                var source = "https://api.nuget.org/v3/index.json";

                var path = UriUtility.GetAbsolutePath(root, source);

                Assert.Equal(source, path);
            }
        }

        [Fact]
        public void UriUtility_GetAbsolutePath_VerifyAbsolutePathUnchanged()
        {
            using (var root = TestDirectory.Create())
            using (var root2 = TestDirectory.Create())
            {
                var source = root2;

                var path = UriUtility.GetAbsolutePath(root, source);

                Assert.Equal(source, path);
            }
        }

        [Theory]
        [InlineData("http://nuget.org/api/v2", true)]
        [InlineData("http://NUGET.ORG/api/v2", true)]
        [InlineData("https://nuget.org/api/v2", true)]
        [InlineData("https://NUGET.ORG/api/v2", true)]
        [InlineData("http://www.nuget.org/api/v2", true)]
        [InlineData("http://WWW.NUGET.ORG/api/v2", true)]
        [InlineData("https://www.nuget.org/api/v2", true)]
        [InlineData("https://WWW.NUGET.ORG/api/v2", true)]
        [InlineData("http://api.nuget.org/v3/index.json", true)]
        [InlineData("http://API.NUGET.ORG/v3/index.json", true)]
        [InlineData("https://api.nuget.org/v3/index.json", true)]
        [InlineData("https://API.NUGET.ORG/v3/index.json", true)]
        [InlineData("http://notnuget.org/api/v2", false)]
        [InlineData("http://not.nuget.org", true)]
        [InlineData("https://not.nuget.org/", true)]
        [InlineData("https://nuget.org.internal/v3/index.json", false)]
        [InlineData("http://randommynuget.org/", false)]
        [InlineData("https://randommynuget.org/", false)]
        [InlineData("https://www.random-mynuget.org/", false)]
        [InlineData("https://nuget.smbsrc.net/", false)]
        [InlineData("https://nuget.smbsrc.net/nuget.org", false)]
        [InlineData("", false)]
        [InlineData("file://test", false)]
        [InlineData("..\\a", false)]
        public void IsNuGetOrg(string sourceUrl, bool expected)
        {
            // Act
            var actual = UriUtility.IsNuGetOrg(sourceUrl);

            // Assert
            Assert.Equal(expected, actual);
        }
    }

    /// <summary>
    /// Collection for tests that mutate the process-wide current directory. Disabling parallelization stops them from
    /// racing other tests that read <see cref="Directory.GetCurrentDirectory" /> while it is temporarily changed.
    /// </summary>
    [CollectionDefinition(nameof(UriUtilityCurrentDirectoryCollection), DisableParallelization = true)]
    public class UriUtilityCurrentDirectoryCollection
    {
    }

    [Collection(nameof(UriUtilityCurrentDirectoryCollection))]
    public class UriUtilityCurrentDirectoryTests
    {
        // With a fully qualified root, resolution must never depend on the process working directory. Running the
        // same call from two different current directories must produce the same result.
        [Theory]
        [InlineData("sub/child")]
        [InlineData("nested/../child")]
        [InlineData("./relative")]
        [InlineData("https://api.nuget.org/v3/index.json")]
        public void UriUtility_GetAbsolutePath_WithAbsoluteRoot_IsIndependentOfCurrentDirectory(string source)
        {
            using (var root = TestDirectory.Create())
            using (var currentDirectoryA = TestDirectory.Create())
            using (var currentDirectoryB = TestDirectory.Create())
            {
                var resultFromA = RunWithCurrentDirectory(currentDirectoryA, () => UriUtility.GetAbsolutePath(root, source));
                var resultFromB = RunWithCurrentDirectory(currentDirectoryB, () => UriUtility.GetAbsolutePath(root, source));

                Assert.Equal(resultFromA, resultFromB);
            }
        }

#if !NETFRAMEWORK
        // A bare drive specifier such as "C:" is drive-relative on Windows. Path.Combine drops the fully qualified
        // root and Path.GetFullPath("C:") historically resolved it against the current directory of that drive, which
        // leaked the process working directory. The multithread-safe resolution only ships in the .NET build.
        [PlatformFact(Platform.Windows)]
        public void UriUtility_GetAbsolutePath_WithDriveRelativePath_IsIndependentOfCurrentDirectory()
        {
            using (var root = TestDirectory.Create())
            using (var currentDirectoryA = TestDirectory.Create())
            using (var currentDirectoryB = TestDirectory.Create())
            {
                // Use the drive the temp folders live on so moving the current directory below actually changes what
                // a bare drive specifier would otherwise resolve to.
                string? pathRoot = Path.GetPathRoot(currentDirectoryA.Path);
                Assert.False(string.IsNullOrEmpty(pathRoot));
                string driveRelativePath = pathRoot!.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                var resultFromA = RunWithCurrentDirectory(currentDirectoryA, () => UriUtility.GetAbsolutePath(root, driveRelativePath));
                var resultFromB = RunWithCurrentDirectory(currentDirectoryB, () => UriUtility.GetAbsolutePath(root, driveRelativePath));

                Assert.Equal(resultFromA, resultFromB);
                Assert.Equal(Path.GetFullPath(root.Path), resultFromA);
            }
        }
#endif

        private static string? RunWithCurrentDirectory(string currentDirectory, Func<string?> action)
        {
            string originalCurrentDirectory = Directory.GetCurrentDirectory();

            try
            {
                Directory.SetCurrentDirectory(currentDirectory);
                return action();
            }
            finally
            {
                Directory.SetCurrentDirectory(originalCurrentDirectory);
            }
        }
    }
}
