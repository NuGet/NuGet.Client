using System;
using System.Threading.Tasks;
using Xunit;

namespace NuGet.CommandLine.Test.Caching
{
    /// <summary>
    /// The thought behind this test suite is to validate every permutation of test matrix comprised
    /// of the following variables:
    /// - Installation command (implementations of ICachingCommand)
    /// - Caching aspect (implementation of ICachingTest)
    /// - NoCache argument enabled or disabled
    /// - Server type (V2 or V3)
    /// </summary>
    public class CachingTests
    {
        /// <summary>
        /// This is a sanity check test that does not verify any caching.
        /// </summary>
        [Theory]
        [InlineData(typeof(InstallPackagesConfigCommand), CachingType.Default, ServerType.V2)]
        [InlineData(typeof(InstallPackagesConfigCommand), CachingType.Default, ServerType.V3)]
        [InlineData(typeof(InstallPackagesConfigCommand), CachingType.NoCache, ServerType.V2)]
        [InlineData(typeof(InstallPackagesConfigCommand), CachingType.NoCache, ServerType.V3)]
        [InlineData(typeof(InstallSpecificVersionCommand), CachingType.Default, ServerType.V2)]
        [InlineData(typeof(InstallSpecificVersionCommand), CachingType.Default, ServerType.V3)]
        [InlineData(typeof(InstallSpecificVersionCommand), CachingType.NoCache, ServerType.V2)]
        [InlineData(typeof(InstallSpecificVersionCommand), CachingType.NoCache, ServerType.V3)]
        [InlineData(typeof(RestorePackagesConfigCommand), CachingType.Default, ServerType.V2)]
        [InlineData(typeof(RestorePackagesConfigCommand), CachingType.Default, ServerType.V3)]
        [InlineData(typeof(RestorePackagesConfigCommand), CachingType.NoCache, ServerType.V2)]
        [InlineData(typeof(RestorePackagesConfigCommand), CachingType.NoCache, ServerType.V3)]
        [InlineData(typeof(RestoreProjectJsonCommand), CachingType.Default, ServerType.V2)]
        [InlineData(typeof(RestoreProjectJsonCommand), CachingType.Default, ServerType.V3)]
        [InlineData(typeof(RestoreProjectJsonCommand), CachingType.NoCache, ServerType.V2)]
        [InlineData(typeof(RestoreProjectJsonCommand), CachingType.NoCache, ServerType.V3)]
        public async Task NuGetExe_Caching_InstallsToDestinationFolder(Type commandType, CachingType caching, ServerType server)
        {
            // Arrange
            var nuGetExe = await GetNuGetExeAsync();

            // Act
            var validations = await CachingTestRunner.ExecuteAsync(
                typeof(InstallsToDestinationFolderTest),
                commandType,
                nuGetExe,
                caching,
                server);

            // Assert
            validations.Assert(CachingValidationType.CommandSucceeded, true);
            validations.Assert(CachingValidationType.PackageInstalled, true);
        }

        /// <summary>
        /// Inconsistencies tracked here:
        /// https://github.com/NuGet/Home/issues/3244
        /// </summary>
        [Theory]
        [InlineData(typeof(InstallPackagesConfigCommand), CachingType.Default, ServerType.V2, true, true)]
        [InlineData(typeof(InstallPackagesConfigCommand), CachingType.Default, ServerType.V3, true, true)]
        [InlineData(typeof(InstallPackagesConfigCommand), CachingType.NoCache, ServerType.V2, true, false)] // Should either fail or install?
        [InlineData(typeof(InstallPackagesConfigCommand), CachingType.NoCache, ServerType.V3, true, true)] // Should fail?
        [InlineData(typeof(InstallSpecificVersionCommand), CachingType.Default, ServerType.V2, true, true)]
        [InlineData(typeof(InstallSpecificVersionCommand), CachingType.Default, ServerType.V3, true, true)]
        [InlineData(typeof(InstallSpecificVersionCommand), CachingType.NoCache, ServerType.V2, false, false)]
        [InlineData(typeof(InstallSpecificVersionCommand), CachingType.NoCache, ServerType.V3, false, false)]
        [InlineData(typeof(RestorePackagesConfigCommand), CachingType.Default, ServerType.V2, true, true)]
        [InlineData(typeof(RestorePackagesConfigCommand), CachingType.Default, ServerType.V3, true, true)]
        [InlineData(typeof(RestorePackagesConfigCommand), CachingType.NoCache, ServerType.V2, false, false)]
        [InlineData(typeof(RestorePackagesConfigCommand), CachingType.NoCache, ServerType.V3, true, true)] // Should fail?
        [InlineData(typeof(RestoreProjectJsonCommand), CachingType.Default, ServerType.V2, true, true)]
        [InlineData(typeof(RestoreProjectJsonCommand), CachingType.Default, ServerType.V3, true, true)]
        [InlineData(typeof(RestoreProjectJsonCommand), CachingType.NoCache, ServerType.V2, true, true)] // Should fail?
        [InlineData(typeof(RestoreProjectJsonCommand), CachingType.NoCache, ServerType.V3, true, true)] // Should fail?
        public async Task NuGetExe_Caching_AllowsMissingPackageOnSource(Type commandType, CachingType caching, ServerType server, bool success, bool installed)
        {
            // Arrange
            var nuGetExe = await GetNuGetExeAsync();

            // Act
            var validations = await CachingTestRunner.ExecuteAsync(
                typeof(AllowsMissingPackageOnSourceTest),
                commandType,
                nuGetExe,
                caching,
                server);

            // Assert
            validations.Assert(CachingValidationType.CommandSucceeded, success);
            validations.Assert(CachingValidationType.PackageInstalled, installed);
        }

        /// <summary>
        /// There is currently no way to disable populating the global packages folder.
        /// </summary>
        [Theory]
        [InlineData(typeof(InstallPackagesConfigCommand), CachingType.Default, ServerType.V2)]
        [InlineData(typeof(InstallPackagesConfigCommand), CachingType.Default, ServerType.V3)]
        [InlineData(typeof(InstallPackagesConfigCommand), CachingType.NoCache, ServerType.V2)]
        [InlineData(typeof(InstallPackagesConfigCommand), CachingType.NoCache, ServerType.V3)]
        [InlineData(typeof(InstallSpecificVersionCommand), CachingType.Default, ServerType.V2)]
        [InlineData(typeof(InstallSpecificVersionCommand), CachingType.Default, ServerType.V3)]
        [InlineData(typeof(InstallSpecificVersionCommand), CachingType.NoCache, ServerType.V2)]
        [InlineData(typeof(InstallSpecificVersionCommand), CachingType.NoCache, ServerType.V3)]
        [InlineData(typeof(RestorePackagesConfigCommand), CachingType.Default, ServerType.V2)]
        [InlineData(typeof(RestorePackagesConfigCommand), CachingType.Default, ServerType.V3)]
        [InlineData(typeof(RestorePackagesConfigCommand), CachingType.NoCache, ServerType.V2)]
        [InlineData(typeof(RestorePackagesConfigCommand), CachingType.NoCache, ServerType.V3)]
        [InlineData(typeof(RestoreProjectJsonCommand), CachingType.Default, ServerType.V2)]
        [InlineData(typeof(RestoreProjectJsonCommand), CachingType.Default, ServerType.V3)]
        [InlineData(typeof(RestoreProjectJsonCommand), CachingType.NoCache, ServerType.V2)]
        [InlineData(typeof(RestoreProjectJsonCommand), CachingType.NoCache, ServerType.V3)]
        public async Task NuGetExe_Caching_PopulatesGlobalPackagesFolder(Type commandType, CachingType caching, ServerType server)
        {
            // Arrange
            var nuGetExe = await GetNuGetExeAsync();

            // Act
            var validations = await CachingTestRunner.ExecuteAsync(
                typeof(PopulatesGlobalPackagesFolderTest),
                commandType,
                nuGetExe,
                caching,
                server);

            // Assert
            validations.Assert(CachingValidationType.CommandSucceeded, true);
            validations.Assert(CachingValidationType.PackageInGlobalPackagesFolder, true);
        }

        /// <summary>
        /// There is currently no way to disable getting the package from the global packages folder.
        /// </summary>
        [Theory]
        [InlineData(typeof(InstallPackagesConfigCommand), CachingType.Default, ServerType.V2)]
        [InlineData(typeof(InstallPackagesConfigCommand), CachingType.Default, ServerType.V3)]
        [InlineData(typeof(InstallPackagesConfigCommand), CachingType.NoCache, ServerType.V2)]
        [InlineData(typeof(InstallPackagesConfigCommand), CachingType.NoCache, ServerType.V3)]
        [InlineData(typeof(InstallSpecificVersionCommand), CachingType.Default, ServerType.V2)]
        [InlineData(typeof(InstallSpecificVersionCommand), CachingType.Default, ServerType.V3)]
        [InlineData(typeof(InstallSpecificVersionCommand), CachingType.NoCache, ServerType.V2)]
        [InlineData(typeof(InstallSpecificVersionCommand), CachingType.NoCache, ServerType.V3)]
        [InlineData(typeof(RestorePackagesConfigCommand), CachingType.Default, ServerType.V2)]
        [InlineData(typeof(RestorePackagesConfigCommand), CachingType.Default, ServerType.V3)]
        [InlineData(typeof(RestorePackagesConfigCommand), CachingType.NoCache, ServerType.V2)]
        [InlineData(typeof(RestorePackagesConfigCommand), CachingType.NoCache, ServerType.V3)]
        [InlineData(typeof(RestoreProjectJsonCommand), CachingType.Default, ServerType.V2)]
        [InlineData(typeof(RestoreProjectJsonCommand), CachingType.Default, ServerType.V3)]
        [InlineData(typeof(RestoreProjectJsonCommand), CachingType.NoCache, ServerType.V2)]
        [InlineData(typeof(RestoreProjectJsonCommand), CachingType.NoCache, ServerType.V3)]
        public async Task NuGetExe_Caching_UsesGlobalPackagesFolderCopy(Type commandType, CachingType caching, ServerType server)
        {
            // Arrange
            var nuGetExe = await GetNuGetExeAsync();

            // Act
            var validations = await CachingTestRunner.ExecuteAsync(
                typeof(UsesGlobalPackagesFolderCopyTest),
                commandType,
                nuGetExe,
                caching,
                server);

            // Assert
            validations.Assert(CachingValidationType.CommandSucceeded, true);
            validations.Assert(CachingValidationType.PackageInstalled, true);
            validations.Assert(CachingValidationType.PackageFromGlobalPackagesFolderUsed, true);
            validations.Assert(CachingValidationType.PackageFromSourceNotUsed, true);
        }

        /// <summary>
        /// Currently, only project.json restore uses the HTTP cache. Eventually, the
        /// packages.config should use the HTTP cache as well.
        /// https://github.com/NuGet/Home/issues/3132
        /// </summary>
        [Theory]
        [InlineData(typeof(InstallPackagesConfigCommand), CachingType.Default, ServerType.V2, false)]
        [InlineData(typeof(InstallPackagesConfigCommand), CachingType.Default, ServerType.V3, false)]
        [InlineData(typeof(InstallPackagesConfigCommand), CachingType.NoCache, ServerType.V2, false)]
        [InlineData(typeof(InstallPackagesConfigCommand), CachingType.NoCache, ServerType.V3, false)]
        [InlineData(typeof(InstallSpecificVersionCommand), CachingType.Default, ServerType.V2, false)]
        [InlineData(typeof(InstallSpecificVersionCommand), CachingType.Default, ServerType.V3, false)]
        [InlineData(typeof(InstallSpecificVersionCommand), CachingType.NoCache, ServerType.V2, false)]
        [InlineData(typeof(InstallSpecificVersionCommand), CachingType.NoCache, ServerType.V3, false)]
        [InlineData(typeof(RestorePackagesConfigCommand), CachingType.Default, ServerType.V2, false)]
        [InlineData(typeof(RestorePackagesConfigCommand), CachingType.Default, ServerType.V3, false)]
        [InlineData(typeof(RestorePackagesConfigCommand), CachingType.NoCache, ServerType.V2, false)]
        [InlineData(typeof(RestorePackagesConfigCommand), CachingType.NoCache, ServerType.V3, false)]
        [InlineData(typeof(RestoreProjectJsonCommand), CachingType.Default, ServerType.V2, true)]
        [InlineData(typeof(RestoreProjectJsonCommand), CachingType.Default, ServerType.V3, true)]
        [InlineData(typeof(RestoreProjectJsonCommand), CachingType.NoCache, ServerType.V2, false)]
        [InlineData(typeof(RestoreProjectJsonCommand), CachingType.NoCache, ServerType.V3, false)]
        public async Task NuGetExe_Caching_UsesHttpCacheCopy(Type commandType, CachingType caching, ServerType server, bool success)
        {
            // Arrange
            var nuGetExe = await GetNuGetExeAsync();

            // Act
            var validations = await CachingTestRunner.ExecuteAsync(
                typeof(UsesHttpCacheCopyTest),
                commandType,
                nuGetExe,
                caching,
                server);

            // Assert
            validations.Assert(CachingValidationType.CommandSucceeded, true);
            validations.Assert(CachingValidationType.PackageInstalled, true);
            validations.Assert(CachingValidationType.PackageFromHttpCacheUsed, success);
            validations.Assert(CachingValidationType.PackageFromSourceNotUsed, success);
        }

        /// <summary>
        /// Currently, only project.json restore uses the HTTP cache. Eventually, the
        /// packages.config should use the HTTP cache as well.
        /// https://github.com/NuGet/Home/issues/3132
        /// </summary>
        [Theory]
        [InlineData(typeof(InstallPackagesConfigCommand), CachingType.Default, ServerType.V2, false)]
        [InlineData(typeof(InstallPackagesConfigCommand), CachingType.Default, ServerType.V3, false)]
        [InlineData(typeof(InstallPackagesConfigCommand), CachingType.NoCache, ServerType.V2, false)]
        [InlineData(typeof(InstallPackagesConfigCommand), CachingType.NoCache, ServerType.V3, false)]
        [InlineData(typeof(InstallSpecificVersionCommand), CachingType.Default, ServerType.V2, false)]
        [InlineData(typeof(InstallSpecificVersionCommand), CachingType.Default, ServerType.V3, false)]
        [InlineData(typeof(InstallSpecificVersionCommand), CachingType.NoCache, ServerType.V2, false)]
        [InlineData(typeof(InstallSpecificVersionCommand), CachingType.NoCache, ServerType.V3, false)]
        [InlineData(typeof(RestorePackagesConfigCommand), CachingType.Default, ServerType.V2, false)]
        [InlineData(typeof(RestorePackagesConfigCommand), CachingType.Default, ServerType.V3, false)]
        [InlineData(typeof(RestorePackagesConfigCommand), CachingType.NoCache, ServerType.V2, false)]
        [InlineData(typeof(RestorePackagesConfigCommand), CachingType.NoCache, ServerType.V3, false)]
        [InlineData(typeof(RestoreProjectJsonCommand), CachingType.Default, ServerType.V2, true)]
        [InlineData(typeof(RestoreProjectJsonCommand), CachingType.Default, ServerType.V3, true)]
        [InlineData(typeof(RestoreProjectJsonCommand), CachingType.NoCache, ServerType.V2, false)]
        [InlineData(typeof(RestoreProjectJsonCommand), CachingType.NoCache, ServerType.V3, false)]
        public async Task NuGetExe_Caching_WritesToHttpCacheTest(Type commandType, CachingType caching, ServerType server, bool success)
        {
            // Arrange
            var nuGetExe = await GetNuGetExeAsync();

            // Act
            var validations = await CachingTestRunner.ExecuteAsync(
                typeof(WritesToHttpCacheTest),
                commandType,
                nuGetExe,
                caching,
                server);

            // Assert
            validations.Assert(CachingValidationType.CommandSucceeded, true);
            validations.Assert(CachingValidationType.PackageInHttpCache, success);
        }

        private static async Task<INuGetExe> GetNuGetExeAsync()
        {
            await Task.Yield();
            
            return NuGetExe.GetBuiltNuGetExe();
        }
    }
}
