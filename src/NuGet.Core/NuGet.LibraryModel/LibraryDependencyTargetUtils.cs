// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.LibraryModel
{
    /// <summary>
    /// Helper methods for dealing with LibraryDependencyTarget strings.
    /// </summary>
    public class LibraryDependencyTargetUtils
    {
        /// <summary>
        /// Convert flag string into a LibraryTypeFlag.
        /// </summary>
        public static LibraryDependencyTarget Parse(string flag)
        {
            // If the LibraryDependency does not have a flag value it is considered all
            if (string.IsNullOrEmpty(flag))
            {
                return LibraryDependencyTarget.All;
            }

            var pieces = flag
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(f => f.Trim().ToLowerInvariant())
                .Where(f => f.Length > 0)
                .ToList();

            if (!pieces.Any())
            {
                return LibraryDependencyTarget.All;
            }

            return pieces
                .Select(f => ParseSingleFlag(f))
                .Aggregate(LibraryDependencyTarget.None, (a, b) => a | b);
        }

        private static LibraryDependencyTarget ParseSingleFlag(string flag)
        {
            switch (flag.ToLowerInvariant())
            {
                case "package":
                    return LibraryDependencyTarget.Package;
                case "project":
                    return LibraryDependencyTarget.Project;
                case "externalproject":
                    return LibraryDependencyTarget.ExternalProject;
                case "reference":
                    return LibraryDependencyTarget.Reference;
                case "assembly":
                    return LibraryDependencyTarget.Assembly;
                case "winmd":
                    return LibraryDependencyTarget.WinMD;
                case "all":
                    return LibraryDependencyTarget.All;
                default:
                    return LibraryDependencyTarget.None;
            }
        }

        /// <summary>
        /// Convert type flags to a friendly string.
        /// </summary>
        public static string GetFlagString(LibraryDependencyTarget flags)
        {
            if (flags == LibraryDependencyTarget.None)
            {
                return "none";
            }

            if (flags == LibraryDependencyTarget.All)
            {
                return "all";
            }

            var flagStrings = new List<string>();

            foreach (LibraryDependencyTarget value in Enum.GetValues(typeof(LibraryDependencyTarget)))
            {
                if (value != LibraryDependencyTarget.None && flags.HasFlag(value))
                {
                    flagStrings.Add(value.ToString().ToLowerInvariant());
                }
            }

            return string.Join(",", flagStrings);
        }
    }
}
