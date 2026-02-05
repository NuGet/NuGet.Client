// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Client;
using NuGet.Frameworks;
using NuGet.Packaging.Core;

namespace NuGet.ContentModel
{
    /// <summary>
    /// Classifies package assets using a decision tree approach instead of pattern matching.
    /// This provides O(n * d) complexity where d is tree depth (~4-5) instead of O(n * m) 
    /// where m is the number of patterns.
    /// </summary>
    internal sealed class AssetClassifier
    {
        private readonly Dictionary<ReadOnlyMemory<char>, NuGetFramework> _frameworkCache;
        private readonly PatternTable? _dotnetAnyTable;
        private readonly PatternTable? _anyTable;

        private static readonly NuGetFramework NetTfm = new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Net, FrameworkConstants.EmptyVersion);
        private static readonly NuGetFramework DotNetAnyFramework = FrameworkConstants.CommonFrameworks.DotNet;

        /// <summary>
        /// File extensions for managed assemblies.
        /// </summary>
        private static readonly string[] AssemblyExtensions = { ".dll", ".winmd", ".exe" };

        /// <summary>
        /// File extensions for MSBuild files.
        /// </summary>
        private static readonly string[] MSBuildExtensions = { ".targets", ".props" };

        /// <summary>
        /// File extension for satellite assemblies.
        /// </summary>
        private const string SatelliteAssemblyExtension = ".resources.dll";

        public AssetClassifier(
            Dictionary<ReadOnlyMemory<char>, NuGetFramework> frameworkCache,
            PatternTable? dotnetAnyTable,
            PatternTable? anyTable)
        {
            _frameworkCache = frameworkCache;
            _dotnetAnyTable = dotnetAnyTable;
            _anyTable = anyTable;
        }

        /// <summary>
        /// Classifies an asset path and returns the asset type along with a ContentItem if matched.
        /// </summary>
        /// <param name="path">The asset path to classify.</param>
        /// <param name="assetType">The type of asset if matched.</param>
        /// <returns>A ContentItem if the path matches a known pattern, null otherwise.</returns>
        public ContentItem? Classify(string path, out AssetType assetType)
        {
            assetType = AssetType.None;

            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            int firstDelimiter = path.IndexOf('/');
            if (firstDelimiter <= 0 || firstDelimiter >= path.Length - 1)
            {
                return null;
            }

            ReadOnlySpan<char> root = path.AsSpan(0, firstDelimiter);
            int nextStart = firstDelimiter + 1;

            // Dispatch based on first path segment
            if (root.Equals("lib", StringComparison.OrdinalIgnoreCase))
            {
                return ClassifyLib(path, nextStart, out assetType);
            }
            else if (root.Equals("ref", StringComparison.OrdinalIgnoreCase))
            {
                return ClassifyRef(path, nextStart, out assetType);
            }
            else if (root.Equals("runtimes", StringComparison.OrdinalIgnoreCase))
            {
                return ClassifyRuntimes(path, nextStart, out assetType);
            }
            else if (root.Equals("build", StringComparison.OrdinalIgnoreCase))
            {
                return ClassifyBuild(path, nextStart, out assetType);
            }
            else if (root.Equals("buildMultiTargeting", StringComparison.OrdinalIgnoreCase))
            {
                return ClassifyBuildMultiTargeting(path, nextStart, out assetType);
            }
            else if (root.Equals("buildCrossTargeting", StringComparison.OrdinalIgnoreCase))
            {
                // Deprecated, but still supported
                return ClassifyBuildMultiTargeting(path, nextStart, out assetType);
            }
            else if (root.Equals("buildTransitive", StringComparison.OrdinalIgnoreCase))
            {
                return ClassifyBuildTransitive(path, nextStart, out assetType);
            }
            else if (root.Equals("contentFiles", StringComparison.OrdinalIgnoreCase))
            {
                return ClassifyContentFiles(path, nextStart, out assetType);
            }
            else if (root.Equals("tools", StringComparison.OrdinalIgnoreCase))
            {
                return ClassifyTools(path, nextStart, out assetType);
            }
            else if (root.Equals("embed", StringComparison.OrdinalIgnoreCase))
            {
                return ClassifyEmbed(path, nextStart, out assetType);
            }

            return null;
        }

        /// <summary>
        /// Classifies lib/{tfm}/{assembly} or lib/{assembly} patterns.
        /// Handles RuntimeAssemblies, CompileLibAssemblies, and ResourceAssemblies.
        /// </summary>
        private ContentItem? ClassifyLib(string path, int startIndex, out AssetType assetType)
        {
            assetType = AssetType.None;

            int nextDelimiter = FindNextDelimiter(path, startIndex);
            if (nextDelimiter == -1)
            {
                // lib/{assembly} - legacy pattern without TFM
                ReadOnlyMemory<char> assemblyPart = path.AsMemory(startIndex);
                if (TryParseAssembly(assemblyPart, out object? assemblyValue))
                {
                    assetType = AssetType.RuntimeAssembly;
                    return CreateContentItem(path, tfm: NetTfm, tfmRaw: "net0", assembly: assemblyValue);
                }
                return null;
            }

            ReadOnlyMemory<char> tfmPart = path.AsMemory(startIndex, nextDelimiter - startIndex);
            NuGetFramework? tfm = ParseTargetFramework(tfmPart, _dotnetAnyTable);
            if (tfm == null)
            {
                return null;
            }

            int afterTfm = nextDelimiter + 1;
            int thirdDelimiter = FindNextDelimiter(path, afterTfm);

            if (thirdDelimiter == -1)
            {
                // lib/{tfm}/{assembly}
                ReadOnlyMemory<char> assemblyPart = path.AsMemory(afterTfm);
                if (TryParseAssembly(assemblyPart, out object? assemblyValue))
                {
                    assetType = AssetType.RuntimeAssembly;
                    return CreateContentItem(path, tfm: tfm, tfmRaw: tfmPart.ToString(), assembly: assemblyValue);
                }
                return null;
            }

            // lib/{tfm}/{locale}/{satelliteAssembly} - Resource assembly pattern
            ReadOnlyMemory<char> localePart = path.AsMemory(afterTfm, thirdDelimiter - afterTfm);
            object? localeValue = ParseLocale(localePart);
            if (localeValue != null)
            {
                ReadOnlyMemory<char> satellitePart = path.AsMemory(thirdDelimiter + 1);
                if (TryParseSatelliteAssembly(satellitePart, out object? satelliteValue))
                {
                    assetType = AssetType.ResourceAssembly;
                    return CreateContentItem(path, tfm: tfm, tfmRaw: tfmPart.ToString(), locale: localeValue, satelliteAssembly: satelliteValue);
                }
            }

            return null;
        }

        /// <summary>
        /// Classifies ref/{tfm}/{assembly} patterns for CompileRefAssemblies.
        /// </summary>
        private ContentItem? ClassifyRef(string path, int startIndex, out AssetType assetType)
        {
            assetType = AssetType.None;

            int nextDelimiter = FindNextDelimiter(path, startIndex);
            if (nextDelimiter == -1)
            {
                return null;
            }

            ReadOnlyMemory<char> tfmPart = path.AsMemory(startIndex, nextDelimiter - startIndex);
            NuGetFramework? tfm = ParseTargetFramework(tfmPart, _dotnetAnyTable);
            if (tfm == null)
            {
                return null;
            }

            int afterTfm = nextDelimiter + 1;
            if (afterTfm >= path.Length)
            {
                return null;
            }

            ReadOnlyMemory<char> assemblyPart = path.AsMemory(afterTfm);
            if (TryParseAssembly(assemblyPart, out object? assemblyValue))
            {
                assetType = AssetType.CompileRefAssembly;
                return CreateContentItem(path, tfm: tfm, tfmRaw: tfmPart.ToString(), assembly: assemblyValue);
            }

            return null;
        }

        /// <summary>
        /// Classifies runtimes/{rid}/... patterns.
        /// Handles RuntimeAssemblies, NativeLibraries, and ResourceAssemblies.
        /// </summary>
        private ContentItem? ClassifyRuntimes(string path, int startIndex, out AssetType assetType)
        {
            assetType = AssetType.None;

            // runtimes/{rid}/...
            int ridDelimiter = FindNextDelimiter(path, startIndex);
            if (ridDelimiter == -1)
            {
                return null;
            }

            ReadOnlyMemory<char> ridPart = path.AsMemory(startIndex, ridDelimiter - startIndex);
            string rid = ridPart.ToString();

            int typeStart = ridDelimiter + 1;
            int typeDelimiter = FindNextDelimiter(path, typeStart);
            if (typeDelimiter == -1)
            {
                return null;
            }

            ReadOnlySpan<char> typePart = path.AsSpan(typeStart, typeDelimiter - typeStart);

            if (typePart.Equals("lib", StringComparison.OrdinalIgnoreCase))
            {
                // runtimes/{rid}/lib/{tfm}/{assembly} or runtimes/{rid}/lib/{tfm}/{locale}/{satelliteAssembly}
                return ClassifyRuntimesLib(path, typeDelimiter + 1, rid, out assetType);
            }
            else if (typePart.Equals("native", StringComparison.OrdinalIgnoreCase))
            {
                // runtimes/{rid}/native/{any}
                return ClassifyRuntimesNative(path, typeDelimiter + 1, rid, out assetType);
            }
            else if (typePart.Equals("nativeassets", StringComparison.OrdinalIgnoreCase))
            {
                // runtimes/{rid}/nativeassets/{tfm}/{any}
                return ClassifyRuntimesNativeAssets(path, typeDelimiter + 1, rid, out assetType);
            }

            return null;
        }

        private ContentItem? ClassifyRuntimesLib(string path, int startIndex, string rid, out AssetType assetType)
        {
            assetType = AssetType.None;

            int tfmDelimiter = FindNextDelimiter(path, startIndex);
            if (tfmDelimiter == -1)
            {
                return null;
            }

            ReadOnlyMemory<char> tfmPart = path.AsMemory(startIndex, tfmDelimiter - startIndex);
            NuGetFramework? tfm = ParseTargetFramework(tfmPart, _dotnetAnyTable);
            if (tfm == null)
            {
                return null;
            }

            int afterTfm = tfmDelimiter + 1;
            int nextDelimiter = FindNextDelimiter(path, afterTfm);

            if (nextDelimiter == -1)
            {
                // runtimes/{rid}/lib/{tfm}/{assembly}
                ReadOnlyMemory<char> assemblyPart = path.AsMemory(afterTfm);
                if (TryParseAssembly(assemblyPart, out object? assemblyValue))
                {
                    assetType = AssetType.RuntimeAssembly;
                    return CreateContentItem(path, tfm: tfm, tfmRaw: tfmPart.ToString(), rid: rid, assembly: assemblyValue);
                }
                return null;
            }

            // runtimes/{rid}/lib/{tfm}/{locale}/{satelliteAssembly}
            ReadOnlyMemory<char> localePart = path.AsMemory(afterTfm, nextDelimiter - afterTfm);
            object? localeValue = ParseLocale(localePart);
            if (localeValue != null)
            {
                ReadOnlyMemory<char> satellitePart = path.AsMemory(nextDelimiter + 1);
                if (TryParseSatelliteAssembly(satellitePart, out object? satelliteValue))
                {
                    assetType = AssetType.ResourceAssembly;
                    return CreateContentItem(path, tfm: tfm, tfmRaw: tfmPart.ToString(), rid: rid, locale: localeValue, satelliteAssembly: satelliteValue);
                }
            }

            return null;
        }

        private static ContentItem? ClassifyRuntimesNative(string path, int startIndex, string rid, out AssetType assetType)
        {
            assetType = AssetType.None;

            if (startIndex >= path.Length)
            {
                return null;
            }

            // runtimes/{rid}/native/{any} - TFM defaults to AnyFramework
            ReadOnlyMemory<char> anyPart = path.AsMemory(startIndex);
            assetType = AssetType.NativeLibrary;
            return CreateContentItem(path, tfm: AnyFramework.AnyFramework, tfmRaw: "any", rid: rid, any: anyPart.ToString());
        }

        private ContentItem? ClassifyRuntimesNativeAssets(string path, int startIndex, string rid, out AssetType assetType)
        {
            assetType = AssetType.None;

            int tfmDelimiter = FindNextDelimiter(path, startIndex);
            if (tfmDelimiter == -1)
            {
                return null;
            }

            ReadOnlyMemory<char> tfmPart = path.AsMemory(startIndex, tfmDelimiter - startIndex);
            NuGetFramework? tfm = ParseTargetFramework(tfmPart, _dotnetAnyTable);
            if (tfm == null)
            {
                return null;
            }

            int afterTfm = tfmDelimiter + 1;
            if (afterTfm >= path.Length)
            {
                return null;
            }

            // runtimes/{rid}/nativeassets/{tfm}/{any}
            ReadOnlyMemory<char> anyPart = path.AsMemory(afterTfm);
            assetType = AssetType.NativeLibrary;
            return CreateContentItem(path, tfm: tfm, tfmRaw: tfmPart.ToString(), rid: rid, any: anyPart.ToString());
        }

        /// <summary>
        /// Classifies build/{tfm}/{msbuild} or build/{msbuild} patterns.
        /// </summary>
        private ContentItem? ClassifyBuild(string path, int startIndex, out AssetType assetType)
        {
            assetType = AssetType.None;

            int nextDelimiter = FindNextDelimiter(path, startIndex);
            if (nextDelimiter == -1)
            {
                // build/{msbuild} - no TFM
                ReadOnlyMemory<char> msbuildPart = path.AsMemory(startIndex);
                if (TryParseMSBuild(msbuildPart, out object? msbuildValue))
                {
                    assetType = AssetType.MSBuildFile;
                    return CreateContentItem(path, tfm: AnyFramework.AnyFramework, tfmRaw: "any", msbuild: msbuildValue);
                }
                return null;
            }

            ReadOnlyMemory<char> tfmPart = path.AsMemory(startIndex, nextDelimiter - startIndex);
            NuGetFramework? tfm = ParseTargetFramework(tfmPart, _dotnetAnyTable);
            if (tfm == null)
            {
                // First segment might be msbuild file directly: build/{msbuild}
                if (TryParseMSBuild(tfmPart, out object? msbuildValue))
                {
                    assetType = AssetType.MSBuildFile;
                    return CreateContentItem(path, tfm: AnyFramework.AnyFramework, tfmRaw: "any", msbuild: msbuildValue);
                }
                return null;
            }

            int afterTfm = nextDelimiter + 1;
            if (afterTfm >= path.Length)
            {
                return null;
            }

            // build/{tfm}/{msbuild}
            ReadOnlyMemory<char> msbuildPart2 = path.AsMemory(afterTfm);
            if (TryParseMSBuild(msbuildPart2, out object? msbuildValue2))
            {
                assetType = AssetType.MSBuildFile;
                return CreateContentItem(path, tfm: tfm, tfmRaw: tfmPart.ToString(), msbuild: msbuildValue2);
            }

            return null;
        }

        /// <summary>
        /// Classifies buildMultiTargeting/{msbuild} patterns.
        /// </summary>
        private static ContentItem? ClassifyBuildMultiTargeting(string path, int startIndex, out AssetType assetType)
        {
            assetType = AssetType.None;

            if (startIndex >= path.Length)
            {
                return null;
            }

            ReadOnlyMemory<char> msbuildPart = path.AsMemory(startIndex);
            if (TryParseMSBuild(msbuildPart, out object? msbuildValue))
            {
                assetType = AssetType.MSBuildMultiTargetingFile;
                return CreateContentItem(path, tfm: AnyFramework.AnyFramework, tfmRaw: "any", msbuild: msbuildValue);
            }

            return null;
        }

        /// <summary>
        /// Classifies buildTransitive/{tfm}/{msbuild} or buildTransitive/{msbuild} patterns.
        /// </summary>
        private ContentItem? ClassifyBuildTransitive(string path, int startIndex, out AssetType assetType)
        {
            assetType = AssetType.None;

            int nextDelimiter = FindNextDelimiter(path, startIndex);
            if (nextDelimiter == -1)
            {
                // buildTransitive/{msbuild} - no TFM
                ReadOnlyMemory<char> msbuildPart = path.AsMemory(startIndex);
                if (TryParseMSBuild(msbuildPart, out object? msbuildValue))
                {
                    assetType = AssetType.MSBuildTransitiveFile;
                    return CreateContentItem(path, tfm: AnyFramework.AnyFramework, tfmRaw: "any", msbuild: msbuildValue);
                }
                return null;
            }

            ReadOnlyMemory<char> tfmPart = path.AsMemory(startIndex, nextDelimiter - startIndex);
            NuGetFramework? tfm = ParseTargetFramework(tfmPart, _dotnetAnyTable);
            if (tfm == null)
            {
                // First segment might be msbuild file directly
                if (TryParseMSBuild(tfmPart, out object? msbuildValue))
                {
                    assetType = AssetType.MSBuildTransitiveFile;
                    return CreateContentItem(path, tfm: AnyFramework.AnyFramework, tfmRaw: "any", msbuild: msbuildValue);
                }
                return null;
            }

            int afterTfm = nextDelimiter + 1;
            if (afterTfm >= path.Length)
            {
                return null;
            }

            // buildTransitive/{tfm}/{msbuild}
            ReadOnlyMemory<char> msbuildPart2 = path.AsMemory(afterTfm);
            if (TryParseMSBuild(msbuildPart2, out object? msbuildValue2))
            {
                assetType = AssetType.MSBuildTransitiveFile;
                return CreateContentItem(path, tfm: tfm, tfmRaw: tfmPart.ToString(), msbuild: msbuildValue2);
            }

            return null;
        }

        /// <summary>
        /// Classifies contentFiles/{codeLanguage}/{tfm}/{any} patterns.
        /// </summary>
        private ContentItem? ClassifyContentFiles(string path, int startIndex, out AssetType assetType)
        {
            assetType = AssetType.None;

            // contentFiles/{codeLanguage}/{tfm}/{any?}
            int langDelimiter = FindNextDelimiter(path, startIndex);
            if (langDelimiter == -1)
            {
                return null;
            }

            ReadOnlyMemory<char> langPart = path.AsMemory(startIndex, langDelimiter - startIndex);
            object? codeLanguage = ParseCodeLanguage(langPart);
            if (codeLanguage == null)
            {
                return null;
            }

            int tfmStart = langDelimiter + 1;
            int tfmDelimiter = FindNextDelimiter(path, tfmStart);
            if (tfmDelimiter == -1)
            {
                return null;
            }

            ReadOnlyMemory<char> tfmPart = path.AsMemory(tfmStart, tfmDelimiter - tfmStart);
            NuGetFramework? tfm = ParseTargetFramework(tfmPart, null);
            if (tfm == null)
            {
                return null;
            }

            int afterTfm = tfmDelimiter + 1;
            string? anyValue = afterTfm < path.Length ? path.Substring(afterTfm) : null;

            assetType = AssetType.ContentFile;
            return CreateContentItem(path, tfm: tfm, tfmRaw: tfmPart.ToString(), codeLanguage: codeLanguage, any: anyValue);
        }

        /// <summary>
        /// Classifies tools/{tfm}/{rid}/{any} patterns.
        /// </summary>
        private ContentItem? ClassifyTools(string path, int startIndex, out AssetType assetType)
        {
            assetType = AssetType.None;

            // tools/{tfm}/{rid}/{any?}
            int tfmDelimiter = FindNextDelimiter(path, startIndex);
            if (tfmDelimiter == -1)
            {
                return null;
            }

            ReadOnlyMemory<char> tfmPart = path.AsMemory(startIndex, tfmDelimiter - startIndex);
            NuGetFramework? tfm = ParseTargetFramework(tfmPart, _anyTable);
            if (tfm == null)
            {
                return null;
            }

            int ridStart = tfmDelimiter + 1;
            int ridDelimiter = FindNextDelimiter(path, ridStart);
            if (ridDelimiter == -1)
            {
                return null;
            }

            string rid = path.Substring(ridStart, ridDelimiter - ridStart);

            int afterRid = ridDelimiter + 1;
            string? anyValue = afterRid < path.Length ? path.Substring(afterRid) : null;

            assetType = AssetType.ToolsAssembly;
            return CreateContentItem(path, tfm: tfm, tfmRaw: tfmPart.ToString(), rid: rid, any: anyValue);
        }

        /// <summary>
        /// Classifies embed/{tfm}/{assembly} patterns.
        /// </summary>
        private ContentItem? ClassifyEmbed(string path, int startIndex, out AssetType assetType)
        {
            assetType = AssetType.None;

            int nextDelimiter = FindNextDelimiter(path, startIndex);
            if (nextDelimiter == -1)
            {
                return null;
            }

            ReadOnlyMemory<char> tfmPart = path.AsMemory(startIndex, nextDelimiter - startIndex);
            NuGetFramework? tfm = ParseTargetFramework(tfmPart, _dotnetAnyTable);
            if (tfm == null)
            {
                return null;
            }

            int afterTfm = nextDelimiter + 1;
            if (afterTfm >= path.Length)
            {
                return null;
            }

            ReadOnlyMemory<char> assemblyPart = path.AsMemory(afterTfm);
            if (TryParseAssembly(assemblyPart, out object? assemblyValue))
            {
                assetType = AssetType.EmbedAssembly;
                return CreateContentItem(path, tfm: tfm, tfmRaw: tfmPart.ToString(), assembly: assemblyValue);
            }

            return null;
        }

        #region Helper Methods

        private static int FindNextDelimiter(string path, int startIndex)
        {
            for (int i = startIndex; i < path.Length; i++)
            {
                if (path[i] == '/')
                {
                    return i;
                }
            }
            return -1;
        }

        private NuGetFramework? ParseTargetFramework(ReadOnlyMemory<char> name, PatternTable? table)
        {
            // Check for table replacements (e.g., "any" -> DotNet or AnyFramework)
            if (table != null && table.TryLookup(ManagedCodeConventions.PropertyNames.TargetFrameworkMoniker, name, out object? tableValue))
            {
                return tableValue as NuGetFramework;
            }

            if (name.IsEmpty)
            {
                return null;
            }

            // Check cache
            if (_frameworkCache.TryGetValue(name, out NuGetFramework? cachedResult))
            {
                return cachedResult;
            }

            // Parse the framework
            string nameStr = name.ToString();
            NuGetFramework result = NuGetFramework.ParseFolder(nameStr);

            if (result.IsUnsupported)
            {
                // Fallback to full parsing for legacy support
                result = NuGetFramework.ParseFrameworkName(nameStr, DefaultFrameworkNameProvider.Instance);

                if (result.IsUnsupported)
                {
                    // For unknown frameworks return the name as is
                    result = new NuGetFramework(nameStr, FrameworkConstants.EmptyVersion);
                }
            }

            _frameworkCache[name] = result;
            return result;
        }

        private static bool TryParseAssembly(ReadOnlyMemory<char> name, out object? value)
        {
            value = null;

            // Check for empty folder placeholder
            if (MemoryExtensions.Equals(PackagingCoreConstants.EmptyFolder.AsSpan(), name.Span, StringComparison.Ordinal))
            {
                value = PackagingCoreConstants.EmptyFolder;
                return true;
            }

            // Check file extension
            foreach (string ext in AssemblyExtensions)
            {
                if (name.Span.EndsWith(ext.AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    value = name.ToString();
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseMSBuild(ReadOnlyMemory<char> name, out object? value)
        {
            value = null;

            // Check for empty folder placeholder
            if (MemoryExtensions.Equals(PackagingCoreConstants.EmptyFolder.AsSpan(), name.Span, StringComparison.Ordinal))
            {
                value = PackagingCoreConstants.EmptyFolder;
                return true;
            }

            // Check file extension
            foreach (string ext in MSBuildExtensions)
            {
                if (name.Span.EndsWith(ext.AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    value = name.ToString();
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseSatelliteAssembly(ReadOnlyMemory<char> name, out object? value)
        {
            value = null;

            // Check for empty folder placeholder
            if (MemoryExtensions.Equals(PackagingCoreConstants.EmptyFolder.AsSpan(), name.Span, StringComparison.Ordinal))
            {
                value = PackagingCoreConstants.EmptyFolder;
                return true;
            }

            // Satellite assemblies must end with .resources.dll
            if (name.Span.EndsWith(SatelliteAssemblyExtension.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                value = name.ToString();
                return true;
            }

            return false;
        }

        private static object? ParseLocale(ReadOnlyMemory<char> name)
        {
            // Use the same heuristic as ManagedCodeConventions.Locale_Parser
            if (name.Length == 2 || name.Length == 3)
            {
                return name.ToString();
            }

            // e.g., en-US
            if (name.Length >= 4 && name.Span[2] == '-')
            {
                return name.ToString();
            }

            // e.g., agq-CM
            if (name.Length >= 5 && name.Span[3] == '-')
            {
                return name.ToString();
            }

            return null;
        }

        private static object? ParseCodeLanguage(ReadOnlyMemory<char> name)
        {
            // Code language values must be alphanumeric
            foreach (char c in name.Span)
            {
                if (!char.IsLetterOrDigit(c))
                {
                    return null;
                }
            }

            return name.ToString();
        }

        private static ContentItem CreateContentItem(
            string path,
            NuGetFramework? tfm = null,
            string? tfmRaw = null,
            string? rid = null,
            object? assembly = null,
            object? msbuild = null,
            object? locale = null,
            object? satelliteAssembly = null,
            object? codeLanguage = null,
            string? any = null)
        {
            var item = new ContentItem { Path = path };

            if (tfm != null)
            {
                item._tfm = tfm;
            }
            if (tfmRaw != null)
            {
                item._tfmRaw = tfmRaw;
            }
            if (rid != null)
            {
                item._rid = rid;
            }
            if (assembly != null)
            {
                item._assembly = assembly;
            }
            if (msbuild != null)
            {
                item._msbuild = msbuild;
            }
            if (locale != null)
            {
                item._locale = locale;
            }
            if (satelliteAssembly != null)
            {
                item._satelliteAssembly = satelliteAssembly;
            }
            if (codeLanguage != null)
            {
                item._codeLanguage = codeLanguage;
            }
            if (any != null)
            {
                item._any = any;
            }

            return item;
        }

        #endregion
    }

    /// <summary>
    /// Represents the type of asset in a NuGet package.
    /// </summary>
    internal enum AssetType
    {
        None = 0,
        RuntimeAssembly,
        CompileRefAssembly,
        CompileLibAssembly,
        NativeLibrary,
        ResourceAssembly,
        MSBuildFile,
        MSBuildMultiTargetingFile,
        MSBuildTransitiveFile,
        ContentFile,
        ToolsAssembly,
        EmbedAssembly
    }
}
