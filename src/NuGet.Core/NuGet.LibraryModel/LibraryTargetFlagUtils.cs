// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.LibraryModel
{
    /// <summary>
    /// Helper methods for dealing with Target flag strings.
    /// </summary>
    public class LibraryTargetFlagUtils
    {
        /// <summary>
        /// Package, Project, or ExternalProject
        /// </summary>
        public static readonly LibraryTypeFlag PackageProjectExternal =
            (LibraryTypeFlag.Package | LibraryTypeFlag.Project | LibraryTypeFlag.ExternalProject);

        /// <summary>
        /// Convert flag string into a LibraryTypeFlag.
        /// </summary>
        public static LibraryTypeFlag GetFlag(string flag)
        {
            // If the LibraryDependency does not have a flag value it is considered all
            if (string.IsNullOrEmpty(flag))
            {
                return LibraryTypeFlag.All;
            }

            switch (flag.ToLowerInvariant())
            {
                case "package":
                    return LibraryTypeFlag.Package;
                case "project":
                    return LibraryTypeFlag.Project;
                case "externalproject":
                    return LibraryTypeFlag.ExternalProject;
                case "reference":
                    return LibraryTypeFlag.Reference;
                case "assembly":
                    return LibraryTypeFlag.Assembly;
                case "winmd":
                    return LibraryTypeFlag.WinMD;
                case "all":
                    return LibraryTypeFlag.All;

                    // None is a noop here
            }

            return LibraryTypeFlag.None;
        }

        /// <summary>
        /// Convert type flags to a friendly string.
        /// </summary>
        public static string GetFlagString(LibraryTypeFlag flags)
        {
            if (flags == LibraryTypeFlag.None)
            {
                return "none";
            }

            if (flags == LibraryTypeFlag.All)
            {
                return "all";
            }

            var flagStrings = new List<string>();

            foreach (LibraryTypeFlag value in Enum.GetValues(typeof(LibraryTypeFlag)))
            {
                if (value != LibraryTypeFlag.None && flags.HasFlag(value))
                {
                    flagStrings.Add(value.ToString().ToLowerInvariant());
                }
            }

            return string.Join(",", flagStrings);
        }
    }
}
