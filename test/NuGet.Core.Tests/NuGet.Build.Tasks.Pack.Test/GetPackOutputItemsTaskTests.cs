// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Test.Utility;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Build.Tasks.Pack.Test
{
    public class GetPackOutputItemsTaskTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public static IEnumerable<object[]> PackageFileNameTestCases => PackageFileNameTestCase.TestCases;

        public GetPackOutputItemsTaskTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        // This unit test verifies that GetPackOutputItemsTask outputs the expected file name.
        [Theory]
        [MemberData(nameof(PackageFileNameTestCases))]
        public void GetPackOutputItemsTaskTests_Execute_CheckPackageFileName(PackageFileNameTestCase testCase)
        {
            var outputItemTask = new GetPackOutputItemsTask();
            outputItemTask.PackageId = testCase.IdProjProp;
            outputItemTask.PackageVersion = testCase.VersionProjProp;
            outputItemTask.IncludeSymbols = testCase.IncludeSymbols;
            outputItemTask.SymbolPackageFormat = PackageFileNameTestsCommon.GetSymbolPackageFormatText(testCase.SymbolPackageFormat);
            outputItemTask.OutputFileNamesWithoutVersion = testCase.OutputFileNamesWithoutVersion;
            if (!string.IsNullOrWhiteSpace(testCase.VersionNuspecProperties))
            {
                outputItemTask.NuspecProperties = new string[] { $"version={testCase.VersionNuspecProperties}" };
            }

            using (var testDirectory = TestDirectory.Create())
            {
                outputItemTask.PackageOutputPath = testDirectory.Path;
                outputItemTask.NuspecOutputPath = testDirectory.Path;
                if (testCase.UseNuspecFile)
                {
                    outputItemTask.NuspecFile = System.IO.Path.Combine(testDirectory.Path, PackageFileNameTestsCommon.FILENAME_NUSPEC_FILE);
                }

                PackageFileNameTestsCommon.CreateNuspecFile(testCase, testDirectory);

                Assert.True(outputItemTask.Execute());

                foreach (string outputNupkgName in testCase.OutputNupkgNames)
                {
                    string[] itemSpecs = outputItemTask.OutputPackItems.Select(item => item.ItemSpec).ToArray();
                    var matchCount = PackageFileNameTestsCommon.GetNameMatchFilePathCount(outputNupkgName, itemSpecs);
                    Assert.True(matchCount == 1, $"{outputNupkgName} is not found in output. [{string.Join(" , ", itemSpecs.Select(_ => System.IO.Path.GetFileName(_)))}]");
                }
            }
        }
    }
}
