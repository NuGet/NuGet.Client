﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Packaging
{
    public static class PackagingConstants
    {
        public static readonly string AnyFramework = "any";
        public static readonly string AgnosticFramework = "agnostic";

        public static readonly string TargetFrameworkPropertyKey = "targetframework";

        public static readonly string ContentFilesDefaultBuildAction = "Compile";

        public static class Folders
        {
            public static readonly string Content = "content";
            public static readonly string Build = "build";
            public static readonly string Tools = "tools";
            public static readonly string ContentFiles = "contentFiles";
            public static readonly string Lib = "lib";
            public static readonly string Native = "native";
            public static readonly string Runtimes = "runtimes";
            public static readonly string Ref = "ref";
            public static readonly string Analyzers = "analyzers";

            public static string[] Known { get; } = new string[]
            {
                Content,
                Build,
                Tools,
                ContentFiles,
                Lib,
                Native,
                Runtimes,
                Ref,
                Analyzers
            };
        }

        /// <summary>
        /// Represents the ".nuspec" extension.
        /// </summary>
        public static readonly string ManifestExtension = ".nuspec";

        // Starting from nuget 2.0, we use a file with the special name '_._' to represent an empty folder.
        internal const string PackageEmptyFileName = "_._";
    }
}
