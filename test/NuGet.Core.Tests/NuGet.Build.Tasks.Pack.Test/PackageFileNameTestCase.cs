// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using NuGet.Commands;
using Xunit.Abstractions;

namespace NuGet.Build.Tasks.Pack.Test
{
    /// <remarks>
    /// <see cref="GetPackOutputItemsTaskTests"/>
    /// </remarks>
    public class PackageFileNameTestCase : IXunitSerializable
    {
        private string _scenario = string.Empty;
        private string[] _outputNupkgNames = System.Array.Empty<string>();
        private string _idProjProp = string.Empty;
        private string _idNuspecProperties = string.Empty;
        private string _idNuspecMeta = string.Empty;
        private string _versionProjProp = string.Empty;
        private string _versionNuspecProperties = string.Empty;
        private string _versionNuspecMeta = string.Empty;
        private bool _useNuspecFile;
        private bool _outputFileNamesWithoutVersion;
        private bool _includeSymbols;
        private SymbolPackageFormat _symbolPackageFormat = SymbolPackageFormat.Snupkg;

        public static System.Collections.Generic.IEnumerable<object[]> TestCases
        {
            get
            {
                var cases = new PackageFileNameTestCase[]
                    {

                        //// without nuspec input
                        new() { Scenario = "NoNuspec_NormalizesShortVersion", OutputNupkgNames = ["proj.1.9.0.nupkg"], IdProjProp = "proj", VersionProjProp = "1.9", UseNuspecFile = false },
                        new() { Scenario = "NoNuspec_TrimsTrailingRevisionZero", OutputNupkgNames = ["proj.2.0.0.nupkg"], IdProjProp = "proj", VersionProjProp = "2.0.0.0", UseNuspecFile = false },
                        new() { Scenario = "NoNuspec_PreservesNonZeroRevision", OutputNupkgNames = ["proj.2.0.0.1.nupkg"], IdProjProp = "proj", VersionProjProp = "2.0.0.1", UseNuspecFile = false },
                        new() { Scenario = "NoNuspec_PreservesFourPartPrereleaseVersion", OutputNupkgNames = ["proj.2.0.0.3-preview.nupkg"], IdProjProp = "proj", VersionProjProp = "2.0.0.3-preview", UseNuspecFile = false },
                        new() { Scenario = "NoNuspec_StripsVersionWhenConfigured", OutputNupkgNames = ["proj.nupkg"], IdProjProp = "proj", VersionProjProp = "1.9", UseNuspecFile = false, OutputFileNamesWithoutVersion = true },

                        // with nuspec input
                        new() { Scenario = "WithNuspec_UsesMetadataVersion", OutputNupkgNames = ["nusp.4.0.0.nupkg"], IdProjProp = "proj", VersionProjProp = "2.0.0.0", UseNuspecFile = true, IdNuspecMeta = "nusp", VersionNuspecProperties = "       ", VersionNuspecMeta = "4.0.0.0" },
                        new() { Scenario = "WithNuspec_UsesMetadataRevision", OutputNupkgNames = ["nusp.4.0.0.3.nupkg"], IdProjProp = "proj", VersionProjProp = "2.0.0.3", UseNuspecFile = true, IdNuspecMeta = "nusp", VersionNuspecProperties = "       ", VersionNuspecMeta = "4.0.0.3" },
                        new() { Scenario = "WithNuspec_UsesNuspecPropertiesVersion", OutputNupkgNames = ["nusp.3.0.0.4.nupkg"], IdProjProp = "proj", VersionProjProp = "2.0.0.4", UseNuspecFile = true, IdNuspecMeta = "nusp", VersionNuspecProperties = "3.0.0.4", VersionNuspecMeta = "4.0.0.4" },
                        new() { Scenario = "WithNuspec_UsesMetadataPrereleaseVersion", OutputNupkgNames = ["nusp.5.0.0-preview.nupkg"], IdProjProp = "proj", VersionProjProp = "2.0.0.0", UseNuspecFile = true, IdNuspecMeta = "nusp", VersionNuspecProperties = "       ", VersionNuspecMeta = "5.0.0.0-preview" },
                        new() { Scenario = "WithNuspec_UsesMetadataFourPartPrereleaseVersion", OutputNupkgNames = ["nusp.5.0.0.2-preview.nupkg"], IdProjProp = "proj", VersionProjProp = "2.0.0.0", UseNuspecFile = true, IdNuspecMeta = "nusp", VersionNuspecProperties = "       ", VersionNuspecMeta = "5.0.0.2-preview" },
                        new() { Scenario = "WithNuspec_UsesNuspecPropertiesPrereleaseVersion", OutputNupkgNames = ["nusp.6.0.0-beta.nupkg"], IdProjProp = "proj", VersionProjProp = "2.0.0.0", UseNuspecFile = true, IdNuspecMeta = "nusp", VersionNuspecProperties = "6-beta ", VersionNuspecMeta = "5.0.0.3-preview" },
                        new() { Scenario = "WithNuspec_StripsVersionWhenConfigured", OutputNupkgNames = ["nusp.nupkg"], IdProjProp = "proj", VersionProjProp = "2.0.0.0", UseNuspecFile = true, IdNuspecMeta = "nusp", VersionNuspecProperties = "       ", VersionNuspecMeta = "4.0.0.0", OutputFileNamesWithoutVersion = true },

                        // Pinned regression case: PackTask treats NuspecProperties=id=... as $id$ substitution only;
                        // a literal <id> in the nuspec wins. GetPackOutputItemsTask must agree with that.
                        new() { Scenario = "WithNuspec_IdInNuspecPropertiesDoesNotOverrideLiteralId", OutputNupkgNames = ["nusp.4.0.0.nupkg"], IdProjProp = "proj", VersionProjProp = "2.0.0.0", UseNuspecFile = true, IdNuspecMeta = "nusp", IdNuspecProperties = "shouldBeIgnored", VersionNuspecProperties = "       ", VersionNuspecMeta = "4.0.0.0" },

                        // has symbol
                        new() { Scenario = "NoNuspec_SnupkgUsesNormalizedVersion", OutputNupkgNames = ["proj.2.1.0.snupkg"], IdProjProp = "proj", VersionProjProp = "2.1.0.0", UseNuspecFile = false, IncludeSymbols = true, SymbolPackageFormat = NuGet.Commands.SymbolPackageFormat.Snupkg },
                        new() { Scenario = "WithNuspec_SnupkgUsesNuspecPropertiesVersion", OutputNupkgNames = ["nusp.7.1.2.snupkg"], IdProjProp = "proj", VersionProjProp = "2.0.0.0", UseNuspecFile = true, IdNuspecMeta = "nusp", VersionNuspecProperties = "7.1.2", VersionNuspecMeta = "5.0.0.4-preview", IncludeSymbols = true, SymbolPackageFormat = NuGet.Commands.SymbolPackageFormat.Snupkg },
                        new() { Scenario = "NoNuspec_SnupkgStripsVersionWhenConfigured", OutputNupkgNames = ["proj.snupkg"], IdProjProp = "proj", VersionProjProp = "2.1.0.0", UseNuspecFile = false, OutputFileNamesWithoutVersion = true, IncludeSymbols = true, SymbolPackageFormat = NuGet.Commands.SymbolPackageFormat.Snupkg },
                        new() { Scenario = "WithNuspec_SnupkgStripsVersionWhenConfigured", OutputNupkgNames = ["nusp.snupkg"], IdProjProp = "proj", VersionProjProp = "2.0.0.0", UseNuspecFile = true, IdNuspecMeta = "nusp", VersionNuspecProperties = "7.1.2", VersionNuspecMeta = "5.0.0.4-preview", OutputFileNamesWithoutVersion = true, IncludeSymbols = true, SymbolPackageFormat = NuGet.Commands.SymbolPackageFormat.Snupkg },

                        new() { Scenario = "NoNuspec_SymbolsNupkgIncludesPrimaryAndSymbolsPackages", OutputNupkgNames = ["proj.2.2.0.nupkg", "proj.2.2.0.symbols.nupkg"], IdProjProp = "proj", VersionProjProp = "2.2.0.0", UseNuspecFile = false, IncludeSymbols = true, SymbolPackageFormat = NuGet.Commands.SymbolPackageFormat.SymbolsNupkg },
                        new() { Scenario = "WithNuspec_SymbolsNupkgIncludesPrimaryAndSymbolsPackages", OutputNupkgNames = ["nusp.7.2.2.nupkg", "nusp.7.2.2.symbols.nupkg"], IdProjProp = "proj", VersionProjProp = "2.0.0.0", UseNuspecFile = true, IdNuspecMeta = "nusp", VersionNuspecProperties = "7.2.2", VersionNuspecMeta = "5.0.0.4-preview", IncludeSymbols = true, SymbolPackageFormat = NuGet.Commands.SymbolPackageFormat.SymbolsNupkg },
                        new() { Scenario = "NoNuspec_SymbolsNupkgStripsVersionWhenConfigured", OutputNupkgNames = ["proj.nupkg", "proj.symbols.nupkg"], IdProjProp = "proj", VersionProjProp = "2.2.0.0", UseNuspecFile = false, OutputFileNamesWithoutVersion = true, IncludeSymbols = true, SymbolPackageFormat = NuGet.Commands.SymbolPackageFormat.SymbolsNupkg },
                        new() { Scenario = "WithNuspec_SymbolsNupkgStripsVersionWhenConfigured", OutputNupkgNames = ["nusp.nupkg", "nusp.symbols.nupkg"], IdProjProp = "proj", VersionProjProp = "2.0.0.0", UseNuspecFile = true, IdNuspecMeta = "nusp", VersionNuspecProperties = "7.2.2", VersionNuspecMeta = "5.0.0.4-preview", OutputFileNamesWithoutVersion = true, IncludeSymbols = true, SymbolPackageFormat = NuGet.Commands.SymbolPackageFormat.SymbolsNupkg },

                    };


                return cases.Select(c => new object[] { c }).ToArray();
            }
        }

        public string Scenario
        {
            get => _scenario;
            init => _scenario = value;
        }

        public string[] OutputNupkgNames
        {
            get => _outputNupkgNames;
            init => _outputNupkgNames = value;
        }

        public string IdProjProp
        {
            get => _idProjProp;
            init => _idProjProp = value;
        }

        public string IdNuspecMeta
        {
            get => _idNuspecMeta;
            init => _idNuspecMeta = value;
        }

        public string IdNuspecProperties
        {
            get => _idNuspecProperties;
            init => _idNuspecProperties = value;
        }

        public string VersionProjProp
        {
            get => _versionProjProp;
            init => _versionProjProp = value;
        }

        public string VersionNuspecProperties
        {
            get => _versionNuspecProperties;
            init => _versionNuspecProperties = value;
        }

        public string VersionNuspecMeta
        {
            get => _versionNuspecMeta;
            init => _versionNuspecMeta = value;
        }

        public bool UseNuspecFile
        {
            get => _useNuspecFile;
            init => _useNuspecFile = value;
        }

        public bool OutputFileNamesWithoutVersion
        {
            get => _outputFileNamesWithoutVersion;
            init => _outputFileNamesWithoutVersion = value;
        }

        public bool IncludeSymbols
        {
            get => _includeSymbols;
            init => _includeSymbols = value;
        }

        public SymbolPackageFormat SymbolPackageFormat
        {
            get => SymbolPackageFormat1;
            init => SymbolPackageFormat1 = value;
        }
        public SymbolPackageFormat SymbolPackageFormat1 { get => SymbolPackageFormat2; set => SymbolPackageFormat2 = value; }
        public SymbolPackageFormat SymbolPackageFormat2 { get => _symbolPackageFormat; set => _symbolPackageFormat = value; }

        public override string ToString()
        {
            return Scenario;
        }
        public PackageFileNameTestCase()
        {
        }

        void IXunitSerializable.Serialize(IXunitSerializationInfo info)
        {
            info.AddValue(nameof(Scenario), Scenario);
            info.AddValue(nameof(OutputNupkgNames), OutputNupkgNames);
            info.AddValue(nameof(IdProjProp), IdProjProp);
            info.AddValue(nameof(IdNuspecMeta), IdNuspecMeta);
            info.AddValue(nameof(IdNuspecProperties), IdNuspecProperties);
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
            _scenario = (string)info.GetValue(nameof(Scenario), typeof(string));
            _outputNupkgNames = (string[])info.GetValue(nameof(OutputNupkgNames), typeof(string[]));
            _idProjProp = (string)info.GetValue(nameof(IdProjProp), typeof(string));
            _idNuspecMeta = (string)info.GetValue(nameof(IdNuspecMeta), typeof(string));
            _idNuspecProperties = (string)info.GetValue(nameof(IdNuspecProperties), typeof(string));
            _versionProjProp = (string)info.GetValue(nameof(VersionProjProp), typeof(string));
            _versionNuspecProperties = (string)info.GetValue(nameof(VersionNuspecProperties), typeof(string));
            _versionNuspecMeta = (string)info.GetValue(nameof(VersionNuspecMeta), typeof(string));
            _useNuspecFile = (bool)info.GetValue(nameof(UseNuspecFile), typeof(bool));
            _outputFileNamesWithoutVersion = (bool)info.GetValue(nameof(OutputFileNamesWithoutVersion), typeof(bool));
            _includeSymbols = (bool)info.GetValue(nameof(IncludeSymbols), typeof(bool));
            SymbolPackageFormat1 = (NuGet.Commands.SymbolPackageFormat)info.GetValue(nameof(SymbolPackageFormat), typeof(NuGet.Commands.SymbolPackageFormat));
        }
    }

    internal static class PackageFileNameTestsCommon
    {
        public const string FILENAME_PROJECT_FILE = "test.csproj";
        public const string FILENAME_NUSPEC_FILE = "test.nuspec";

        public static void CreateNuspecFile(
            PackageFileNameTestCase testCase,
            string testDirectory)
        {
            if (!testCase.UseNuspecFile)
            {
                return;
            }

            var nuspecPath = Path.Combine(testDirectory, FILENAME_NUSPEC_FILE);
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

            File.WriteAllText(nuspecPath, nuspecContent, new System.Text.UTF8Encoding(true));
        }

        public static string GetSymbolPackageFormatText(NuGet.Commands.SymbolPackageFormat symbolPackageFormat)
        {
            switch (symbolPackageFormat)
            {
                case NuGet.Commands.SymbolPackageFormat.Snupkg: return "snupkg";
                case NuGet.Commands.SymbolPackageFormat.SymbolsNupkg: return "symbols.nupkg";
                default: throw new System.ArgumentOutOfRangeException();
            }
        }

        public static string[] GetOutputExtensions(bool includeSymbols, NuGet.Commands.SymbolPackageFormat symbolPackageFormat)
        {
            if (includeSymbols)
            {
                switch (symbolPackageFormat)
                {
                    case NuGet.Commands.SymbolPackageFormat.Snupkg: return new string[] { ".snupkg" };
                    case NuGet.Commands.SymbolPackageFormat.SymbolsNupkg: return new string[] { ".nupkg", ".symbols.nupkg" };
                    default: throw new System.ArgumentOutOfRangeException();
                }
            }
            else
            {
                return new string[] { ".nupkg" };
            }
        }

        public static int GetNameMatchFilePathCount(string fileName, System.Collections.Generic.IEnumerable<string> fullpaths)
        {
            return fullpaths.Count(file => string.Equals(fileName, System.IO.Path.GetFileName(file), System.StringComparison.OrdinalIgnoreCase));
        }
    }
}
