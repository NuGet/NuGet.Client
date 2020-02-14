// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.LibraryModel
{
    public class LibraryDependencyTypeKeyword
    {
        private static readonly ConcurrentDictionary<string, LibraryDependencyTypeKeyword> Keywords = new ConcurrentDictionary<string, LibraryDependencyTypeKeyword>(StringComparer.OrdinalIgnoreCase);

        public static readonly LibraryDependencyTypeKeyword Default;
        public static readonly LibraryDependencyTypeKeyword Platform;
        public static readonly LibraryDependencyTypeKeyword Build;
        public static readonly LibraryDependencyTypeKeyword Preprocess;
        public static readonly LibraryDependencyTypeKeyword Private;
        public static readonly LibraryDependencyTypeKeyword Dev;

        private readonly string _value;

        public IEnumerable<LibraryDependencyTypeFlag> FlagsToAdd { get; private set; }
        public IEnumerable<LibraryDependencyTypeFlag> FlagsToRemove { get; private set; }

        static LibraryDependencyTypeKeyword()
        {
            var emptyFlags = Enumerable.Empty<LibraryDependencyTypeFlag>();

            Default = Declare(
                "default",
                flagsToAdd: new[]
                    {
                        LibraryDependencyTypeFlag.MainReference,
                        LibraryDependencyTypeFlag.MainSource,
                        LibraryDependencyTypeFlag.MainExport,
                        LibraryDependencyTypeFlag.RuntimeComponent,
                        LibraryDependencyTypeFlag.BecomesNupkgDependency,
                    },
                flagsToRemove: emptyFlags);

            Platform = Declare(
                "platform",
                flagsToAdd: new[]
                    {
                        LibraryDependencyTypeFlag.MainReference,
                        LibraryDependencyTypeFlag.MainSource,
                        LibraryDependencyTypeFlag.MainExport,
                        LibraryDependencyTypeFlag.RuntimeComponent,
                        LibraryDependencyTypeFlag.BecomesNupkgDependency,
                        LibraryDependencyTypeFlag.SharedFramework
                    },
                flagsToRemove: emptyFlags);

            Private = Declare(
                "private",
                flagsToAdd: new[]
                    {
                        LibraryDependencyTypeFlag.MainReference,
                        LibraryDependencyTypeFlag.MainSource,
                        LibraryDependencyTypeFlag.RuntimeComponent,
                        LibraryDependencyTypeFlag.BecomesNupkgDependency,
                    },
                flagsToRemove: emptyFlags);

            Dev = Declare(
                "dev",
                flagsToAdd: new[]
                    {
                        LibraryDependencyTypeFlag.DevComponent,
                    },
                flagsToRemove: emptyFlags);

            Build = Declare(
                "build",
                flagsToAdd: new[]
                    {
                        LibraryDependencyTypeFlag.MainSource,
                        LibraryDependencyTypeFlag.PreprocessComponent,
                    },
                flagsToRemove: emptyFlags);

            Preprocess = Declare(
                "preprocess",
                flagsToAdd: new[]
                    {
                        LibraryDependencyTypeFlag.PreprocessReference,
                    },
                flagsToRemove: emptyFlags);

            DeclareOnOff("MainReference", LibraryDependencyTypeFlag.MainReference, emptyFlags);
            DeclareOnOff("MainSource", LibraryDependencyTypeFlag.MainSource, emptyFlags);
            DeclareOnOff("MainExport", LibraryDependencyTypeFlag.MainExport, emptyFlags);
            DeclareOnOff("PreprocessReference", LibraryDependencyTypeFlag.PreprocessReference, emptyFlags);
            DeclareOnOff("SharedFramework", LibraryDependencyTypeFlag.SharedFramework, emptyFlags);

            DeclareOnOff("RuntimeComponent", LibraryDependencyTypeFlag.RuntimeComponent, emptyFlags);
            DeclareOnOff("DevComponent", LibraryDependencyTypeFlag.DevComponent, emptyFlags);
            DeclareOnOff("PreprocessComponent", LibraryDependencyTypeFlag.PreprocessComponent, emptyFlags);
            DeclareOnOff("BecomesNupkgDependency", LibraryDependencyTypeFlag.BecomesNupkgDependency, emptyFlags);
        }

        public LibraryDependencyType CreateType()
        {
            return LibraryDependencyType.Default.Combine(FlagsToAdd, FlagsToRemove);
        }

        private static void DeclareOnOff(string name, LibraryDependencyTypeFlag flag, IEnumerable<LibraryDependencyTypeFlag> emptyFlags)
        {
            Declare(name,
                flagsToAdd: new[] { flag },
                flagsToRemove: emptyFlags);

            Declare(
                name + "-off",
                flagsToAdd: emptyFlags,
                flagsToRemove: new[] { flag });
        }

        private LibraryDependencyTypeKeyword(
            string value,
            IEnumerable<LibraryDependencyTypeFlag> flagsToAdd,
            IEnumerable<LibraryDependencyTypeFlag> flagsToRemove)
        {
            _value = value;
            FlagsToAdd = flagsToAdd;
            FlagsToRemove = flagsToRemove;
        }

        internal static LibraryDependencyTypeKeyword Declare(
            string keyword,
            IEnumerable<LibraryDependencyTypeFlag> flagsToAdd,
            IEnumerable<LibraryDependencyTypeFlag> flagsToRemove)
        {
            return Keywords.GetOrAdd(keyword, _ => new LibraryDependencyTypeKeyword(keyword, flagsToAdd, flagsToRemove));
        }

        internal static LibraryDependencyTypeKeyword Parse(string keyword)
        {
            LibraryDependencyTypeKeyword value;
            if (Keywords.TryGetValue(keyword?.Trim(), out value))
            {
                return value;
            }
            throw new Exception(string.Format("Unsupported type: {0}", keyword));
        }
    }
}
