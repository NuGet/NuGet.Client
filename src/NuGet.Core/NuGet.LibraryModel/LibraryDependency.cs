// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Text;
using NuGet.Versioning;
using System;

namespace NuGet.LibraryModel
{
    public class LibraryDependency
    {
        public LibraryRange LibraryRange { get; set; }

        public LibraryDependencyType Type { get; set; } = LibraryDependencyType.Default;

        public LibraryIncludeFlags IncludeType { get; set; } = LibraryIncludeFlags.All;

        public LibraryIncludeFlags SuppressParent { get; set; } = LibraryIncludeFlagUtils.DefaultSuppressParent;

        public string Name
        {
            get { return LibraryRange.Name; }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(LibraryRange);
            sb.Append(" ");
            sb.Append(Type);
            sb.Append(" ");
            sb.Append(LibraryIncludeFlagUtils.GetFlagString(IncludeType));
            return sb.ToString();
        }

        /// <summary>
        /// Type property flag
        /// </summary>
        public bool HasFlag(LibraryDependencyTypeFlag flag)
        {
            return Type.Contains(flag);
        }

        public static void AddLibraryDependency(LibraryDependency dependency, ISet<LibraryDependency> list)
        {
            if (list.Any(r => r.Name == dependency.Name))
            {
                var matchingDependency = list.Single(r => r.Name == dependency.Name);
                VersionRange newVersionRange = VersionRange.CommonSubSet(new VersionRange[]
                {
                            matchingDependency.LibraryRange.VersionRange, dependency.LibraryRange.VersionRange
                });
                if (!newVersionRange.Equals(VersionRange.None))
                {
                    list.Remove(matchingDependency);
                    list.Add(new LibraryDependency()
                    {
                        LibraryRange = new LibraryRange(matchingDependency.Name, newVersionRange, LibraryDependencyTarget.All),
                        IncludeType = dependency.IncludeType,
                        SuppressParent = dependency.SuppressParent

                    });
                }
                else
                {
                    //TODO: Add right exception message here
                    throw new Exception("Your package version constraints are messed up.");
                }
            }
            else
            {
                list.Add(dependency);
            }
        }
    }
}
