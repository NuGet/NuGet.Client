// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
#nullable enable

using System.IO;
using System.Linq;
using Microsoft.Internal.NuGet.Testing.SignedPackages.ChildProcess;
using NuGet.Test.Utility;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Build.Tasks.Pack.Test
{

    /// <summary></summary>
    /// <remarks>The _GetOutputItemsFromPack target inside the NuGet.Build.Tasks.Pack.targets file is the subject of the test, so the project that includes NuGet.Build.Tasks.Pack.targets must be registered as a build dependency.</remarks>
    [Collection(FixtureCollection.Name)]
    public class PackageFileNameTests
    {
        #region constructor and fields

#if DEBUG
        const string CONFIGURATION = "Debug";
#else
        const string CONFIGURATION = "Release";
#endif

        const string FILENAME_PROJECT_FILE = "test.csproj";
        const string FILENAME_NUSPEC_FILE = "test.nuspec";

        private readonly BuildFixture _testFixture;
        private readonly ITestOutputHelper _testOutputHelper;

        public PackageFileNameTests(BuildFixture testFixture, ITestOutputHelper testOutputHelper)
        {
            _testFixture = testFixture;
            _testOutputHelper = testOutputHelper;
        }

        #endregion

        public static System.Collections.Generic.IEnumerable<object[]> TestCases
        {
            get
            {
                var cases = new PackageFileNameTestCase[]
                    {

                    // without nuspec input
                    new PackageFileNameTestCase("000",["proj.1.9.0.nupkg"          ], "proj", "nusp", "1.9", "", "", false),
                    new PackageFileNameTestCase("001",["proj.2.0.0.nupkg"          ], "proj", "nusp", "2.0.0.0", "       ", "4.0.0.0", false),
                    new PackageFileNameTestCase("002",["proj.2.0.0.1.nupkg"        ], "proj", "nusp", "2.0.0.1", "       ", "4.0.0.1", false),
                    new PackageFileNameTestCase("003",["proj.2.0.0.2.nupkg"        ], "proj", "nusp", "2.0.0.2", "3.0.0.2", "4.0.0.2", false),
                    new PackageFileNameTestCase("004",["proj.2.0.0.3-preview.nupkg"], "proj", "nusp", "2.0.0.3-preview", "3.0.0.2", "4.0.0.2", false),
                    new PackageFileNameTestCase("005",["proj.2.0.0.4-release.nupkg"], "proj", "$meta_id$", "2.0.0.4-release", "3.0.0.2", "$meta_version$", false),
                    new PackageFileNameTestCase("100",["proj.nupkg"                ], "proj", "nusp", "1.9", "", "", false, outputFileNamesWithoutVersion:true),
                    new PackageFileNameTestCase("104",["proj.nupkg"                ], "proj", "nusp", "2.0.0.3-preview", "3.0.0.2", "4.0.0.2", false,outputFileNamesWithoutVersion:true),

                    // with nuspec input
                    new PackageFileNameTestCase("010",["nusp.4.0.0.nupkg"          ], "proj", "nusp", "2.0.0.0", "       ", "4.0.0.0", true),
                    new PackageFileNameTestCase("011",["nusp.4.0.0.3.nupkg"        ], "proj", "nusp", "2.0.0.3", "       ", "4.0.0.3", true),
                    new PackageFileNameTestCase("012",["nusp.3.0.0.4.nupkg"        ], "proj", "nusp", "2.0.0.4", "3.0.0.4", "4.0.0.4", true),
                    new PackageFileNameTestCase("013",["nusp.5.0.0-preview.nupkg"  ], "proj", "nusp", "2.0.0.0", "       ", "5.0.0.0-preview", true),
                    new PackageFileNameTestCase("014",["nusp.5.0.0.2-preview.nupkg"], "proj", "nusp", "2.0.0.0", "       ", "5.0.0.2-preview", true),
                    new PackageFileNameTestCase("015",["nusp.6.0.0-beta.nupkg"     ], "proj", "nusp", "2.0.0.0", "6-beta ", "5.0.0.3-preview", true),
                    new PackageFileNameTestCase("110",["nusp.nupkg"                ], "proj", "nusp", "2.0.0.0", "       ", "4.0.0.0", true, outputFileNamesWithoutVersion:true),
                    new PackageFileNameTestCase("115",["nusp.nupkg"                ], "proj", "nusp", "2.0.0.0", "6-beta ", "5.0.0.3-preview", true, outputFileNamesWithoutVersion:true),

                    // has symbol
                    new PackageFileNameTestCase("020",["proj.2.1.0.snupkg"], "proj", "nusp", "2.1.0.0", "7.1.1", "5.0.0.3-preview", false, includeSymbols: true, symbolPackageFormat: NuGet.Commands.SymbolPackageFormat.Snupkg ),
                    new PackageFileNameTestCase("021",["nusp.7.1.2.snupkg"], "proj", "nusp", "2.0.0.0", "7.1.2", "5.0.0.4-preview", true, includeSymbols: true, symbolPackageFormat: NuGet.Commands.SymbolPackageFormat.Snupkg ),
                    new PackageFileNameTestCase("120",["proj.snupkg"      ], "proj", "nusp", "2.1.0.0", "7.1.1", "5.0.0.3-preview", false, includeSymbols: true, symbolPackageFormat: NuGet.Commands.SymbolPackageFormat.Snupkg ,outputFileNamesWithoutVersion:true),
                    new PackageFileNameTestCase("121",["nusp.snupkg"      ], "proj", "nusp", "2.0.0.0", "7.1.2", "5.0.0.4-preview", true, includeSymbols: true, symbolPackageFormat: NuGet.Commands.SymbolPackageFormat.Snupkg ,outputFileNamesWithoutVersion:true),

                    new PackageFileNameTestCase("022",["proj.2.2.0.nupkg", "proj.2.2.0.symbols.nupkg"], "proj", "nusp", "2.2.0.0", "7.1.1", "5.0.0.3-preview", false, includeSymbols: true, symbolPackageFormat: NuGet.Commands.SymbolPackageFormat.SymbolsNupkg ),
                    new PackageFileNameTestCase("023",["nusp.7.2.2.nupkg", "nusp.7.2.2.symbols.nupkg"], "proj", "nusp", "2.0.0.0", "7.2.2", "5.0.0.4-preview", true, includeSymbols: true, symbolPackageFormat: NuGet.Commands.SymbolPackageFormat.SymbolsNupkg ),
                    new PackageFileNameTestCase("122",["proj.nupkg", "proj.symbols.nupkg"            ], "proj", "nusp", "2.2.0.0", "7.1.1", "5.0.0.3-preview", false, includeSymbols: true, symbolPackageFormat: NuGet.Commands.SymbolPackageFormat.SymbolsNupkg, outputFileNamesWithoutVersion:true),
                    new PackageFileNameTestCase("123",["nusp.nupkg", "nusp.symbols.nupkg"            ], "proj", "nusp", "2.0.0.0", "7.2.2", "5.0.0.4-preview", true, includeSymbols: true, symbolPackageFormat: NuGet.Commands.SymbolPackageFormat.SymbolsNupkg, outputFileNamesWithoutVersion:true),

                    };


                return (object[][])cases.Select((c, i) => new object[] { c }).ToArray();
            }
        }

        // This unit test verifies that GetPackOutputItemsTask outputs the expected file name.
        [Theory]
        [MemberData(nameof(TestCases))]
        public void GetPackOutputItemsTask_PackageFileName(PackageFileNameTestCase testCase)
        {
            var outputItemTask = new NuGet.Build.Tasks.Pack.GetPackOutputItemsTask();
            outputItemTask.PackageId = testCase.IdProjProp;
            outputItemTask.PackageVersion = testCase.VersionProjProp;
            outputItemTask.IncludeSymbols = testCase.IncludeSymbols;
            outputItemTask.SymbolPackageFormat = GetSymbolPackageFormatText(testCase.SymbolPackageFormat);
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
                    outputItemTask.NuspecFile = System.IO.Path.Combine(testDirectory.Path, FILENAME_NUSPEC_FILE);
                }

                CreateTestProjectFileAndNuspecFile(testDirectory, FILENAME_PROJECT_FILE, FILENAME_NUSPEC_FILE, testCase);

                Assert.True(outputItemTask.Execute());

                foreach (string outputNupkgName in testCase.OutputNupkgNames)
                {
                    string[] itemSpecs = outputItemTask.OutputPackItems.Select(item => item.ItemSpec).ToArray();
                    var matchCount = GetNameMatchFilePathCount(outputNupkgName, itemSpecs);
                    Assert.True(matchCount == 1, $"{outputNupkgName} is not found in output. [{string.Join(" , ", itemSpecs.Select(_ => System.IO.Path.GetFileName(_)))}]");
                }
            }
        }


        // This unit test verifies that the output file names from GetPackOutputItemsTask matches the output file names from PackTask.
        // Since PackTask does not expose the output file name as a property,
        // the current implementation performs an actual build and inspects the generated file.
        // If PackTask is updated in the future to return the output file name as a property, this test will no longer need  a build.
        [PlatformTheory(Platform.Windows)]
        [MemberData(nameof(TestCases))]
        public void PackTask_PackageFileName_FromProjectFileWithNuspecFile(PackageFileNameTestCase testCase)
        {
            string[] outputExtensions = GetOutputExtensions(testCase.IncludeSymbols, testCase.SymbolPackageFormat);

            using (var testDirectory = TestDirectory.Create())
            {
                CreateTestProjectFileAndNuspecFile(testDirectory, FILENAME_PROJECT_FILE, FILENAME_NUSPEC_FILE, testCase);

                CommandRunnerResult runresultDotnetPack;
                if (_testFixture._isDotNetFramework)
                {
                    // As noted in #6703, since the .NetStandard2.0 library has been removed,
                    // running tests on .net Framework requires invoking msbuild.exe.
                    runresultDotnetPack = CommandRunner.Run(
                                        _testFixture._pathMSBuildExe,
                                        testDirectory,
                                        $"/t:Restore;Build;Pack /p:Configuration={CONFIGURATION} /p:UsingMicrosoftNetSdk=true {FILENAME_PROJECT_FILE} ",
                                        testOutputHelper: _testOutputHelper);
                }
                else
                {
                    // dotnet build
                    runresultDotnetPack = CommandRunner.Run(
                                        _testFixture._pathDotnetExe,
                                        testDirectory,
                                        $"build -p:Configuration={CONFIGURATION} {FILENAME_PROJECT_FILE}",
                                        environmentVariables: _testFixture._dotnetEnvironments,
                                        testOutputHelper: _testOutputHelper);
                }
                Assert.True(0 == runresultDotnetPack.ExitCode, runresultDotnetPack.Output + " " + runresultDotnetPack.Errors);

                var objFolder = System.IO.Path.Combine(testDirectory, "obj");
                var log = System.IO.File.ReadAllLines(System.IO.Path.Combine(objFolder, "_OutputPackItems.txt"));
                var lines = log.Where(line => !line.StartsWith(objFolder)).ToArray();

                var nupkgGeneratedFiles = outputExtensions
                        .SelectMany(outputExtension => Directory.GetFiles(testDirectory, $"*{outputExtension}", SearchOption.AllDirectories))
                        .Where(line => !line.StartsWith(objFolder))
                        .Distinct().ToArray();
                Assert.Equal(outputExtensions.Length, nupkgGeneratedFiles.Length);

                foreach (string outputNupkgName in testCase.OutputNupkgNames)
                {
                    var matchCountInFileSystem = GetNameMatchFilePathCount(outputNupkgName, nupkgGeneratedFiles);
                    Assert.True(matchCountInFileSystem == 1, $"{outputNupkgName} is not found in filesystem. [{string.Join(" , ", nupkgGeneratedFiles.Select(_ => System.IO.Path.GetFileName(_)))}]");

                    var matchCountInOutputPackItems = GetNameMatchFilePathCount(outputNupkgName, lines);
                    Assert.True(matchCountInOutputPackItems == 1, $"{outputNupkgName} is not found in OutputPackItems. [{string.Join(" , ", lines.Select(_ => System.IO.Path.GetFileName(_)))}]");
                }
            }
        }

        private void CreateTestProjectFileAndNuspecFile
            (string testDirectory
            , string projectFileName
            , string nuspecFileName
            , PackageFileNameTestCase testCase)
        {

            var csprojPath = Path.Combine(testDirectory, projectFileName);
            var nuspecPath = Path.Combine(testDirectory, nuspecFileName);

            var csprojContent = $"""
<Project Sdk="Microsoft.NET.Sdk">
    <Import Project="{_testFixture._pathTargetsFile}" />
    <PropertyGroup>
        <NuGetPackTaskAssemblyFile>{_testFixture._pathDllFile}</NuGetPackTaskAssemblyFile>
    </PropertyGroup>

    <PropertyGroup>
        <TargetFramework>{_testFixture._testFrameworkMoniker}</TargetFramework>
        <NoWarn>NU5100;NU5119;CS2008</NoWarn>
    </PropertyGroup>

    <PropertyGroup>
        <IsPackable>true</IsPackable>

        <IncludeBuildOutput>true</IncludeBuildOutput>
        <IncludeBuiltProjectOutputGroup>false</IncludeBuiltProjectOutputGroup>
        <GeneratePackageOnBuild>True</GeneratePackageOnBuild>

        <PackageId>{testCase.IdProjProp}</PackageId>
        <PackageVersion>{testCase.VersionProjProp}</PackageVersion>
        <PackageTags>tagA;tagB</PackageTags>

        <NuspecFile Condition="'{testCase.UseNuspecFile}'=='{true}'" >{nuspecFileName}</NuspecFile>
        <NuspecProperties Condition="'{testCase.VersionNuspecProperties?.Trim()}'!=''" >version={testCase.VersionNuspecProperties}</NuspecProperties>

        <IncludeSymbols>{testCase.IncludeSymbols}</IncludeSymbols>
        <SymbolPackageFormat>{GetSymbolPackageFormatText(testCase.SymbolPackageFormat)}</SymbolPackageFormat>

        <OutputFileNamesWithoutVersion Condition="'{testCase.OutputFileNamesWithoutVersion}'=='{true}'" >{testCase.OutputFileNamesWithoutVersion}</OutputFileNamesWithoutVersion>
    </PropertyGroup>
    <ItemGroup>
        <None Remove="{nuspecFileName}" />
    </ItemGroup>

    <Target Name="write_OutputPackItems" AfterTargets="_GetOutputItemsFromPack" >    
	    <WriteLinesToFile File="obj/_OutputPackItems.txt" Lines="@(_OutputPackItems)" Overwrite="true" Encoding="UTF-8" />
    </Target>
</Project>
""";

            var nuspecContent = $"""
<?xml version="1.0" encoding="utf-8"?>
<package xmlns="http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd">
    <metadata>
        <id>{testCase.IdNuspecMeta}</id>
        <version>{testCase.VersionNuspecMeta?.Trim()}</version>
        <authors>Unit Test</authors>
        <description>Sample Description</description>
        <language>en-US</language>
    </metadata>
</package>
""";

            File.WriteAllText(csprojPath, csprojContent, System.Text.Encoding.Unicode);
            if (testCase.UseNuspecFile)
            {
                File.WriteAllText(nuspecPath, nuspecContent, new System.Text.UTF8Encoding(true));
            }

        }

        private static string GetSymbolPackageFormatText(NuGet.Commands.SymbolPackageFormat symbolPackageFormat)
        {
            switch (symbolPackageFormat)
            {
                case Commands.SymbolPackageFormat.Snupkg: return "snupkg";
                case Commands.SymbolPackageFormat.SymbolsNupkg: return "symbols.nupkg";
                default: throw new System.ArgumentOutOfRangeException();
            }
        }

        private static string[] GetOutputExtensions(bool includeSymbols, NuGet.Commands.SymbolPackageFormat symbolPackageFormat)
        {
            if (includeSymbols)
            {
                switch (symbolPackageFormat)
                {
                    case Commands.SymbolPackageFormat.Snupkg: return new string[] { ".snupkg" };
                    case Commands.SymbolPackageFormat.SymbolsNupkg: return new string[] { ".nupkg", ".symbols.nupkg" };
                    default: throw new System.ArgumentOutOfRangeException();
                }
            }
            else
            {
                return new string[] { ".nupkg" };
            }
        }

        private int GetNameMatchFilePathCount(string fileName, System.Collections.Generic.IEnumerable<string> fullpaths)
        {
            return fullpaths.Count(file => string.Equals(fileName, System.IO.Path.GetFileName(file), System.StringComparison.OrdinalIgnoreCase));
        }
    }

    public class PackageFileNameTestCase
            : IXunitSerializable
    {
        public PackageFileNameTestCase
            (string testNumber
           , string[] outputNupkgNames
           , string idProjProp
           , string idNuspecMeta
           , string versionProjProp
           , string versionNuspecProperties
           , string versionNuspecMeta
           , bool useNuspecFile
           , bool outputFileNamesWithoutVersion = false
           , bool includeSymbols = false
           , NuGet.Commands.SymbolPackageFormat symbolPackageFormat = Commands.SymbolPackageFormat.Snupkg)
        {
            TestNumber = testNumber;
            OutputNupkgNames = outputNupkgNames;
            IdProjProp = idProjProp;
            IdNuspecMeta = idNuspecMeta;
            VersionProjProp = versionProjProp;
            VersionNuspecProperties = versionNuspecProperties;
            VersionNuspecMeta = versionNuspecMeta;
            UseNuspecFile = useNuspecFile;
            OutputFileNamesWithoutVersion = outputFileNamesWithoutVersion;
            IncludeSymbols = includeSymbols;
            SymbolPackageFormat = symbolPackageFormat;
        }
        public string TestNumber { get; set; } = string.Empty;
        public string[] OutputNupkgNames { get; set; } = System.Array.Empty<string>();
        public string IdProjProp { get; set; } = string.Empty;
        public string IdNuspecMeta { get; set; } = string.Empty;
        public string VersionProjProp { get; set; } = string.Empty;
        public string VersionNuspecProperties { get; set; } = string.Empty;
        public string VersionNuspecMeta { get; set; } = string.Empty;
        public bool UseNuspecFile { get; set; }
        public bool OutputFileNamesWithoutVersion { get; set; }
        public bool IncludeSymbols { get; set; }
        public NuGet.Commands.SymbolPackageFormat SymbolPackageFormat { get; set; } = Commands.SymbolPackageFormat.Snupkg;

        #region IXunitSerializable

        [System.Obsolete]
        public PackageFileNameTestCase() : this("", [], "", "", "", "", "", false) { }

        void IXunitSerializable.Serialize(IXunitSerializationInfo info)
        {
            info.AddValue(nameof(TestNumber), TestNumber);
            info.AddValue(nameof(OutputNupkgNames), OutputNupkgNames);
            info.AddValue(nameof(IdProjProp), IdProjProp);
            info.AddValue(nameof(IdNuspecMeta), IdNuspecMeta);
            info.AddValue(nameof(VersionProjProp), VersionProjProp);
            info.AddValue(nameof(VersionNuspecProperties), VersionNuspecProperties);
            info.AddValue(nameof(VersionNuspecMeta), VersionNuspecMeta);
            info.AddValue(nameof(UseNuspecFile), UseNuspecFile);
            info.AddValue(nameof(OutputFileNamesWithoutVersion), OutputFileNamesWithoutVersion);
            info.AddValue(nameof(IncludeSymbols), IncludeSymbols);
            info.AddValue(nameof(SymbolPackageFormat), SymbolPackageFormat);
        }
        void IXunitSerializable.Deserialize(IXunitSerializationInfo info)
        {
            TestNumber = (string)info.GetValue(nameof(TestNumber), typeof(string));
            OutputNupkgNames = (string[])info.GetValue(nameof(OutputNupkgNames), typeof(string[]));
            IdProjProp = (string)info.GetValue(nameof(IdProjProp), typeof(string));
            IdNuspecMeta = (string)info.GetValue(nameof(IdNuspecMeta), typeof(string));
            VersionProjProp = (string)info.GetValue(nameof(VersionProjProp), typeof(string));
            VersionNuspecProperties = (string)info.GetValue(nameof(VersionNuspecProperties), typeof(string));
            VersionNuspecMeta = (string)info.GetValue(nameof(VersionNuspecMeta), typeof(string));
            UseNuspecFile = (bool)info.GetValue(nameof(UseNuspecFile), typeof(bool));
            OutputFileNamesWithoutVersion = (bool)info.GetValue(nameof(OutputFileNamesWithoutVersion), typeof(bool));
            IncludeSymbols = (bool)info.GetValue(nameof(IncludeSymbols), typeof(bool));
            SymbolPackageFormat = (NuGet.Commands.SymbolPackageFormat)info.GetValue(nameof(SymbolPackageFormat), typeof(NuGet.Commands.SymbolPackageFormat));
        }

        #endregion
    }
}
