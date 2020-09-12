// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Threading.Tasks;
using NuGet.CommandLine.XPlat;
using NuGet.Packaging;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.XPlat.FuncTest
{
    [Collection("NuGet XPlat Test Collection")]
    public class XPlatVerifyTests
    {
        private static readonly string DotnetCli = DotnetCliUtil.GetDotnetCli();
        private static readonly string XplatDll = DotnetCliUtil.GetXplatDll();

        [Fact]
        public void Verify_MissingPackagePath_Throws()
        {
            Assert.NotNull(DotnetCli);
            Assert.NotNull(XplatDll);

            string args = "verify";

            // Act
            var result = CommandRunner.Run(
              DotnetCli,
              Directory.GetCurrentDirectory(),
              $"{XplatDll} {args}",
              waitForExit: true);

            // Assert
            DotnetCliUtil.VerifyResultFailure(result, "Value cannot be null. (Parameter 'argument')");
        }

        [Fact]
        public async Task Verify_UnSignedPackage_Fails()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var packageX = XPlatTestUtils.CreatePackage(frameworkString: "netcoreapp3.1");

                // Generate Package
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    testDirectory,
                    PackageSaveMode.Defaultv3,
                    packageX);

                var log = new TestCommandOutputLogger();
                string[] args =
                    {
                        "verify",
                        Path.Combine(testDirectory,"packagex", packageX.Version, "*.nupkg")
                    };

                // Act
                int result = Program.MainInternal(args, log);

                Assert.Equal(1, result);
                Assert.Contains(log.ErrorMessages, msg => msg.Contains("NU3004: The package is not signed."));
            }
        }
    }
}
