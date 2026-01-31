// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
#nullable enable

using System;
using System.IO;
using System.Linq;
using Microsoft.Internal.NuGet.Testing.SignedPackages.ChildProcess;
using NuGet.Frameworks;
using NuGet.Test.Utility;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Build.Tasks.Pack.Test
{
    /// <summary></summary>
    /// <remarks>The _GetOutputItemsFromPack target inside the NuGet.Build.Tasks.Pack.targets file is the subject of the test, so the project that includes NuGet.Build.Tasks.Pack.targets must be registered as a build dependency.</remarks>
    public class PackageFileNameTests
    {
        #region constructor and fields

#if DEBUG
        const string CONFIGURATION = "Debug";
#else
        const string CONFIGURATION = "Release";
#endif

        const string FILENAME_DLL = "NuGet.Build.Tasks.Pack.dll";
        const string FILENAME_TARGETS = "NuGet.Build.Tasks.Pack.targets";
        const string FILENAME_PROJECT_FILE = "test.csproj";
        const string FILENAME_NUSPEC_FILE = "test.nuspec";

        private readonly bool _isDotNetFramework = false;
        private readonly string _testFrameworkMoniker = "netstandard2.0";

        private readonly string _pathDotnetExe = "";
        private readonly string _pathMSBuildExe = "";
        private readonly string _pathDllFile;
        private readonly string _pathTargetsFile;

        private readonly ITestOutputHelper _testOutputHelper;

        public PackageFileNameTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;

            _pathDotnetExe = NuGet.Test.Utility.TestFileSystemUtility.GetDotnetCli();
            _pathMSBuildExe = GetMsBuildExePath();
            _testFrameworkMoniker = GetFrameworkMoniker(typeof(NuGet.Build.Tasks.Pack.GetPackOutputItemsTask), out var isDotNetFramework);
            _isDotNetFramework = isDotNetFramework;

            var dllLocation = typeof(NuGet.Build.Tasks.Pack.GetPackOutputItemsTask).Assembly.Location;
            var artifactsDirectory = NuGet.Test.Utility.TestFileSystemUtility.GetArtifactsDirectoryInRepo();

            var dllDirectory = Path.Combine(artifactsDirectory, "NuGet.Build.Tasks.Pack", "bin", CONFIGURATION, _testFrameworkMoniker);
            if (!System.IO.Directory.Exists(dllDirectory))
            {
                _testFrameworkMoniker = GetFrameworkMoniker(typeof(NuGet.Build.Tasks.Pack.GetPackOutputItemsTask), out var isDotnetFramework);
                dllDirectory = Path.Combine(artifactsDirectory, "NuGet.Build.Tasks.Pack", "bin", CONFIGURATION, _testFrameworkMoniker);
            }

            _pathDllFile = Path.Combine(dllDirectory, FILENAME_DLL);

            // https://github.com/NuGet/NuGet.Client/pull/6712
            // NuGet.Build.Tasks.Pack.targets has been moved to in NuGet.Build.Tasks project.
            // Therefore, NuGet.Build.Tasks project must be built before running this test.
            var tfmTargets = GetFrameworkMoniker(typeof(PackageFileNameTests), out var _);
            _pathTargetsFile = Path.Combine(artifactsDirectory, "NuGet.Build.Tasks", "bin", CONFIGURATION, tfmTargets, FILENAME_TARGETS);
            if (!System.IO.File.Exists(_pathTargetsFile))
            {
                _pathTargetsFile = Path.Combine(artifactsDirectory, "NuGet.Build.Tasks", "bin", CONFIGURATION, _testFrameworkMoniker, FILENAME_TARGETS);
                if (!System.IO.File.Exists(_pathTargetsFile))
                {
                    _pathTargetsFile = Path.Combine(dllDirectory, FILENAME_TARGETS);
                }
            }

            Assert.True(System.IO.File.Exists(_pathDllFile), $"{FILENAME_DLL} missing");
            Assert.True(System.IO.File.Exists(_pathTargetsFile), $"{FILENAME_TARGETS} missing");
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
                    new PackageFileNameTestCase("100",["proj.nupkg"                ], "proj", "nusp", "1.9", "", "", false, OutputFileNamesWithoutVersion:true),
                    new PackageFileNameTestCase("104",["proj.nupkg"                ], "proj", "nusp", "2.0.0.3-preview", "3.0.0.2", "4.0.0.2", false,OutputFileNamesWithoutVersion:true),

                    // with nuspec input
                    new PackageFileNameTestCase("010",["nusp.4.0.0.nupkg"          ], "proj", "nusp", "2.0.0.0", "         ", "4.0.0.0", true),
                    new PackageFileNameTestCase("011",["nusp.4.0.0.3.nupkg"        ], "proj", "nusp", "2.0.0.3", "         ", "4.0.0.3", true),
                    new PackageFileNameTestCase("012",["nusp.3.0.0.4.nupkg"        ], "proj", "nusp", "2.0.0.4", "3.0.0.4", "4.0.0.4", true),
                    new PackageFileNameTestCase("013",["nusp.5.0.0-preview.nupkg"  ], "proj", "nusp", "2.0.0.0", "         ", "5.0.0.0-preview", true),
                    new PackageFileNameTestCase("014",["nusp.5.0.0.2-preview.nupkg"], "proj", "nusp", "2.0.0.0", "         ", "5.0.0.2-preview", true),
                    new PackageFileNameTestCase("015",["nusp.6.0.0-beta.nupkg"     ], "proj", "nusp", "2.0.0.0", "6-beta   ", "5.0.0.3-preview", true),
                    new PackageFileNameTestCase("110",["nusp.nupkg"                ], "proj", "nusp", "2.0.0.0", "         ", "4.0.0.0", true, OutputFileNamesWithoutVersion:true),
                    new PackageFileNameTestCase("115",["nusp.nupkg"                ], "proj", "nusp", "2.0.0.0", "6-beta   ", "5.0.0.3-preview", true, OutputFileNamesWithoutVersion:true),

                    // has symbol
                    new PackageFileNameTestCase("020",["proj.2.1.0.snupkg"], "proj", "nusp", "2.1.0.0", "7.1.1", "5.0.0.3-preview", false, IncludeSymbols: true,SymbolPackageFormat: NuGet.Commands.SymbolPackageFormat.Snupkg ),
                    new PackageFileNameTestCase("021",["nusp.7.1.2.snupkg"], "proj", "nusp", "2.0.0.0", "7.1.2", "5.0.0.4-preview", true, IncludeSymbols: true,SymbolPackageFormat: NuGet.Commands.SymbolPackageFormat.Snupkg ),
                    new PackageFileNameTestCase("120",["proj.snupkg"      ], "proj", "nusp", "2.1.0.0", "7.1.1", "5.0.0.3-preview", false, IncludeSymbols: true,SymbolPackageFormat: NuGet.Commands.SymbolPackageFormat.Snupkg ,OutputFileNamesWithoutVersion:true),
                    new PackageFileNameTestCase("121",["nusp.snupkg"      ], "proj", "nusp", "2.0.0.0", "7.1.2", "5.0.0.4-preview", true, IncludeSymbols: true,SymbolPackageFormat: NuGet.Commands.SymbolPackageFormat.Snupkg ,OutputFileNamesWithoutVersion:true),

                    new PackageFileNameTestCase("022",["proj.2.2.0.nupkg", "proj.2.2.0.symbols.nupkg"], "proj", "nusp", "2.2.0.0", "7.1.1", "5.0.0.3-preview", false, IncludeSymbols: true,SymbolPackageFormat: NuGet.Commands.SymbolPackageFormat.SymbolsNupkg ),
                    new PackageFileNameTestCase("023",["nusp.7.2.2.nupkg", "nusp.7.2.2.symbols.nupkg"], "proj", "nusp", "2.0.0.0", "7.2.2", "5.0.0.4-preview", true, IncludeSymbols: true,SymbolPackageFormat: NuGet.Commands.SymbolPackageFormat.SymbolsNupkg ),
                    new PackageFileNameTestCase("122",["proj.nupkg", "proj.symbols.nupkg"            ], "proj", "nusp", "2.2.0.0", "7.1.1", "5.0.0.3-preview", false, IncludeSymbols: true,SymbolPackageFormat: NuGet.Commands.SymbolPackageFormat.SymbolsNupkg, OutputFileNamesWithoutVersion:true),
                    new PackageFileNameTestCase("123",["nusp.nupkg", "nusp.symbols.nupkg"            ], "proj", "nusp", "2.0.0.0", "7.2.2", "5.0.0.4-preview", true, IncludeSymbols: true,SymbolPackageFormat: NuGet.Commands.SymbolPackageFormat.SymbolsNupkg, OutputFileNamesWithoutVersion:true),
                    // NoVersion
                     };


                return (object[][])cases.Select((c, i) => new object[] { c }).ToArray();
            }
        }

        [Theory]
        [MemberData(nameof(TestCases))]
        public void GetPackOutputItemsTask_PackageFileName(PackageFileNameTestCase testCase)
        {

            const string projectFileName = "test.csproj";
            const string nuspecFileName = "test.nuspec";

            string[] outputExtensions = GetOutputExtensions(testCase.IncludeSymbols, testCase.SymbolPackageFormat);

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
                    outputItemTask.NuspecInputFilePath = System.IO.Path.Combine(testDirectory.Path, nuspecFileName);
                }

                CreateTestProjectFileAndNuspecFile(testDirectory, projectFileName, nuspecFileName, testCase);// idProjProp, idNuspecMeta, versionProjProp, versionNuspecProperties, versionNuspecMeta, useNuspecFile, includeSymbols, symbolPackageFormat);

                Assert.True(outputItemTask.Execute());

                foreach (string outputNupkgName in testCase.OutputNupkgNames)
                {
                    string[] itemSpecs = outputItemTask.OutputPackItems.Select(item => item.ItemSpec).ToArray();
                    var matchCount = GetNameMatchFilePathCount(outputNupkgName, itemSpecs);
                    Assert.True(matchCount == 1, $"{outputNupkgName} is not found in output. [{string.Join(" , ", itemSpecs.Select(_ => System.IO.Path.GetFileName(_)))}]");
                }
            }
        }


        [PlatformTheory(Platform.Windows)]
        [MemberData(nameof(TestCases))]
        public void PackTask_PackageFileName_FromProjectFileWithNuspecFile(PackageFileNameTestCase testCase)
        {
            string[] outputExtensions = GetOutputExtensions(testCase.IncludeSymbols, testCase.SymbolPackageFormat);

            using (var testDirectory = TestDirectory.Create())
            {
                CreateTestProjectFileAndNuspecFile(testDirectory, FILENAME_PROJECT_FILE, FILENAME_NUSPEC_FILE, testCase);// idProjProp, idNuspecMeta, versionProjProp, versionNuspecProperties, versionNuspecMeta, useNuspecFile, includeSymbols, symbolPackageFormat);

                CommandRunnerResult runresultDotnetPack;
                if (_isDotNetFramework)
                {
                    // As noted in #6703, since the .NetStandard2.0 library has been removed,
                    // running tests on .net Framework requires invoking msbuild.exe.
                    runresultDotnetPack = CommandRunner.Run(
                                        _pathMSBuildExe,
                                        testDirectory,
                                        $"/t:Restore;Build;Pack /p:Configuration={CONFIGURATION} /p:UsingMicrosoftNetSdk=true {FILENAME_PROJECT_FILE} ",
                                        testOutputHelper: _testOutputHelper);
                }
                else
                {
                    // dotnet build
                    runresultDotnetPack = CommandRunner.Run(
                                       _pathDotnetExe,
                                       testDirectory,
                                       $"build -p:Configuration={CONFIGURATION} {FILENAME_PROJECT_FILE}",
                                       testOutputHelper: _testOutputHelper);
                }
                Assert.True(0 == runresultDotnetPack.ExitCode, runresultDotnetPack.Output + " " + runresultDotnetPack.Errors);

                var objFolder = System.IO.Path.Combine(testDirectory, "obj");
                var log = System.IO.File.ReadAllLines(System.IO.Path.Combine(objFolder, "_OutputPackItems.txt"));
                var lines = log.Where(line => !line.StartsWith(objFolder)).ToArray();

                var nupkgGneratedFiles = outputExtensions
                        .SelectMany(outputExtension => Directory.GetFiles(testDirectory, $"*{outputExtension}", SearchOption.AllDirectories))
                        .Where(line => !line.StartsWith(objFolder))
                        .Distinct().ToArray();
                Assert.Equal(outputExtensions.Length, nupkgGneratedFiles.Length);

                foreach (string outputNupkgName in testCase.OutputNupkgNames)
                {
                    var matchCountInFileSystem = GetNameMatchFilePathCount(outputNupkgName, nupkgGneratedFiles);
                    Assert.True(matchCountInFileSystem == 1, $"{outputNupkgName} is not found in filesystem. [{string.Join(" , ", nupkgGneratedFiles.Select(_ => System.IO.Path.GetFileName(_)))}]");

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
    <Import Project="{_pathTargetsFile}" />
    <PropertyGroup>
        <NuGetPackTaskAssemblyFile>{_pathDllFile}</NuGetPackTaskAssemblyFile>
    </PropertyGroup>

    <PropertyGroup>
        <TargetFramework>{_testFrameworkMoniker}</TargetFramework>
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
    
        <WriteLinesToFile File="obj/_OutputPackItems2.txt" Lines="$(NuspecFile)" Overwrite="true" Encoding="UTF-8" />
        <WriteLinesToFile File="obj/_OutputPackItems3.txt" Lines="$(NuspecProperties)" Overwrite="true" Encoding="UTF-8" />

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

        private static string GetFrameworkMoniker(Type typeInAssembly, out bool isDotNetFramework)
        {
            var assembly = typeInAssembly.Assembly;
            var targetFrameworkAttribute
                = assembly.GetCustomAttributes(typeof(System.Runtime.Versioning.TargetFrameworkAttribute), false)
                .OfType<System.Runtime.Versioning.TargetFrameworkAttribute>().FirstOrDefault();

            Assert.True(targetFrameworkAttribute != null, "Can't get targetFramework version");

            isDotNetFramework = targetFrameworkAttribute.FrameworkName.Contains(".NETFramework");
            return NuGetFramework.Parse(targetFrameworkAttribute.FrameworkName).GetShortFolderName();
        }

        private static string GetMsBuildExePath()
        {
            if (System.Environment.OSVersion.Platform == System.PlatformID.Win32NT)
            {
                var msbuildexe = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Windows), "Microsoft.NET", "Framework", "v4.0.30319", "msbuild.exe");

                var vswhereexe = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Microsoft Visual Studio", "Installer", "vswhere.exe");
                var runresult = CommandRunner.Run(
                        vswhereexe,
                        System.Environment.CurrentDirectory,
                        @" -latest -find MSBuild\**\Bin\MSBuild.exe");
                if (runresult.Success)
                {
                    msbuildexe = new System.IO.StringReader(runresult.Output).ReadLine() ?? "";
                }
                return msbuildexe;
            }
            else
            {
                return "";
            }
        }
    }

    [method: Newtonsoft.Json.JsonConstructor]
    public record class PackageFileNameTestCase
           (string TestNumber
           , string[] OutputNupkgNames
           , string IdProjProp
           , string IdNuspecMeta
           , string VersionProjProp
           , string VersionNuspecProperties
           , string VersionNuspecMeta
           , bool UseNuspecFile
           , bool OutputFileNamesWithoutVersion = false
           , bool IncludeSymbols = false
           , NuGet.Commands.SymbolPackageFormat SymbolPackageFormat = Commands.SymbolPackageFormat.Snupkg
           ) : IXunitSerializable
    {


        #region IXunitSerializable

        [System.Obsolete]
        public PackageFileNameTestCase() : this("", [], "", "", "", "", "", false) { }

        private const string TestObjectKey = nameof(PackageFileNameTests);
        private readonly Newtonsoft.Json.JsonSerializerSettings _settings = new Newtonsoft.Json.JsonSerializerSettings { MaxDepth = null };

        void IXunitSerializable.Serialize(IXunitSerializationInfo info)
        {
            info.AddValue(TestObjectKey, Newtonsoft.Json.JsonConvert.SerializeObject(this));

        }
        void IXunitSerializable.Deserialize(IXunitSerializationInfo info)
        {
            var json = (string)info.GetValue(TestObjectKey, typeof(string));
            var x = Newtonsoft.Json.JsonConvert.DeserializeObject<PackageFileNameTestCase>(json, _settings);
            if (x == null)
            {
                return;
            }

            var t = typeof(PackageFileNameTestCase);
            var c = t.GetConstructors().Where(_ => _.GetParameters().Length != 0).ToArray()[0];

            foreach (System.Reflection.ParameterInfo p in c.GetParameters())
            {
                var pi = t.GetProperty(p.Name!);
                pi!.SetValue(this, pi.GetValue(x));
            }
        }

        #endregion
    }
}
