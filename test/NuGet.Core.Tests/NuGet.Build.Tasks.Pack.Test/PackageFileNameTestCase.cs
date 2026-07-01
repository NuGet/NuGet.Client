// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NuGet.Commands;
using Xunit.Abstractions;

namespace NuGet.Build.Tasks.Pack.Test
{
    /// <remarks>
    /// <see cref="GetPackOutputItemsTaskTests"/>
    /// </remarks>
    public class PackageFileNameTestCase : IXunitSerializable
    {
        public static IEnumerable<object[]> TestCases
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
                        new() { Scenario = "NoNuspec_SnupkgUsesNormalizedVersion", OutputNupkgNames = ["proj.2.1.0.snupkg"], IdProjProp = "proj", VersionProjProp = "2.1.0.0", UseNuspecFile = false, IncludeSymbols = true, SymbolPackageFormat = SymbolPackageFormat.Snupkg },
                        new() { Scenario = "WithNuspec_SnupkgUsesNuspecPropertiesVersion", OutputNupkgNames = ["nusp.7.1.2.snupkg"], IdProjProp = "proj", VersionProjProp = "2.0.0.0", UseNuspecFile = true, IdNuspecMeta = "nusp", VersionNuspecProperties = "7.1.2", VersionNuspecMeta = "5.0.0.4-preview", IncludeSymbols = true, SymbolPackageFormat = SymbolPackageFormat.Snupkg },
                        new() { Scenario = "NoNuspec_SnupkgStripsVersionWhenConfigured", OutputNupkgNames = ["proj.snupkg"], IdProjProp = "proj", VersionProjProp = "2.1.0.0", UseNuspecFile = false, OutputFileNamesWithoutVersion = true, IncludeSymbols = true, SymbolPackageFormat = SymbolPackageFormat.Snupkg },
                        new() { Scenario = "WithNuspec_SnupkgStripsVersionWhenConfigured", OutputNupkgNames = ["nusp.snupkg"], IdProjProp = "proj", VersionProjProp = "2.0.0.0", UseNuspecFile = true, IdNuspecMeta = "nusp", VersionNuspecProperties = "7.1.2", VersionNuspecMeta = "5.0.0.4-preview", OutputFileNamesWithoutVersion = true, IncludeSymbols = true, SymbolPackageFormat = SymbolPackageFormat.Snupkg },

                        new() { Scenario = "NoNuspec_SymbolsNupkgIncludesPrimaryAndSymbolsPackages", OutputNupkgNames = ["proj.2.2.0.nupkg", "proj.2.2.0.symbols.nupkg"], IdProjProp = "proj", VersionProjProp = "2.2.0.0", UseNuspecFile = false, IncludeSymbols = true, SymbolPackageFormat = SymbolPackageFormat.SymbolsNupkg },
                        new() { Scenario = "WithNuspec_SymbolsNupkgIncludesPrimaryAndSymbolsPackages", OutputNupkgNames = ["nusp.7.2.2.nupkg", "nusp.7.2.2.symbols.nupkg"], IdProjProp = "proj", VersionProjProp = "2.0.0.0", UseNuspecFile = true, IdNuspecMeta = "nusp", VersionNuspecProperties = "7.2.2", VersionNuspecMeta = "5.0.0.4-preview", IncludeSymbols = true, SymbolPackageFormat = SymbolPackageFormat.SymbolsNupkg },
                        new() { Scenario = "NoNuspec_SymbolsNupkgStripsVersionWhenConfigured", OutputNupkgNames = ["proj.nupkg", "proj.symbols.nupkg"], IdProjProp = "proj", VersionProjProp = "2.2.0.0", UseNuspecFile = false, OutputFileNamesWithoutVersion = true, IncludeSymbols = true, SymbolPackageFormat = SymbolPackageFormat.SymbolsNupkg },
                        new() { Scenario = "WithNuspec_SymbolsNupkgStripsVersionWhenConfigured", OutputNupkgNames = ["nusp.nupkg", "nusp.symbols.nupkg"], IdProjProp = "proj", VersionProjProp = "2.0.0.0", UseNuspecFile = true, IdNuspecMeta = "nusp", VersionNuspecProperties = "7.2.2", VersionNuspecMeta = "5.0.0.4-preview", OutputFileNamesWithoutVersion = true, IncludeSymbols = true, SymbolPackageFormat = SymbolPackageFormat.SymbolsNupkg },
                    };


                return [.. cases.Select(c => new object[] { c })];
            }
        }

        public required string Scenario { get; set; }

        public string[] OutputNupkgNames { get; set; } = Array.Empty<string>();

        public string IdProjProp { get; set; } = string.Empty;

        public string IdNuspecMeta { get; set; } = string.Empty;

        public string IdNuspecProperties { get; set; } = string.Empty;

        public string VersionProjProp { get; set; } = string.Empty;

        public string VersionNuspecProperties { get; set; } = string.Empty;

        public string VersionNuspecMeta { get; set; } = string.Empty;

        public bool UseNuspecFile { get; set; }

        public bool OutputFileNamesWithoutVersion { get; set; }

        public bool IncludeSymbols { get; set; }

        public SymbolPackageFormat SymbolPackageFormat { get; set; } = SymbolPackageFormat.Snupkg;

        public override string ToString()
        {
            return Scenario;
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
            Scenario = (string)info.GetValue(nameof(Scenario), typeof(string));
            OutputNupkgNames = (string[])info.GetValue(nameof(OutputNupkgNames), typeof(string[]));
            IdProjProp = (string)info.GetValue(nameof(IdProjProp), typeof(string));
            IdNuspecMeta = (string)info.GetValue(nameof(IdNuspecMeta), typeof(string));
            IdNuspecProperties = (string)info.GetValue(nameof(IdNuspecProperties), typeof(string));
            VersionProjProp = (string)info.GetValue(nameof(VersionProjProp), typeof(string));
            VersionNuspecProperties = (string)info.GetValue(nameof(VersionNuspecProperties), typeof(string));
            VersionNuspecMeta = (string)info.GetValue(nameof(VersionNuspecMeta), typeof(string));
            UseNuspecFile = (bool)info.GetValue(nameof(UseNuspecFile), typeof(bool));
            OutputFileNamesWithoutVersion = (bool)info.GetValue(nameof(OutputFileNamesWithoutVersion), typeof(bool));
            IncludeSymbols = (bool)info.GetValue(nameof(IncludeSymbols), typeof(bool));
            SymbolPackageFormat = (SymbolPackageFormat)info.GetValue(nameof(SymbolPackageFormat), typeof(SymbolPackageFormat));
        }
    }

    internal static class PackageFileNameTestsCommon
    {
        public static void CreateNuspecFile(PackageFileNameTestCase testCase, string testDirectory)
        {
            if (!testCase.UseNuspecFile)
            {
                return;
            }

            var nuspecPath = Path.Combine(testDirectory, "test.nuspec");
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

            File.WriteAllText(nuspecPath, nuspecContent, new UTF8Encoding(true));
        }

        public static string GetSymbolPackageFormatText(SymbolPackageFormat symbolPackageFormat)
        {
            switch (symbolPackageFormat)
            {
                case SymbolPackageFormat.Snupkg: return "snupkg";
                case SymbolPackageFormat.SymbolsNupkg: return "symbols.nupkg";
                default: throw new ArgumentOutOfRangeException();
            }
        }

        public static int GetNameMatchFilePathCount(string fileName, IEnumerable<string> fullpaths)
        {
            return fullpaths.Count(file => string.Equals(fileName, Path.GetFileName(file), StringComparison.OrdinalIgnoreCase));
        }
    }
}
