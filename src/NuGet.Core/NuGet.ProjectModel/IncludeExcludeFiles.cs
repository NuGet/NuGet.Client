// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NuGet.Shared;

namespace NuGet.ProjectModel
{
    public class IncludeExcludeFiles : IEquatable<IncludeExcludeFiles>
    {
        public IReadOnlyList<string> Include { get; set; }
        public IReadOnlyList<string> Exclude { get; set; }
        public IReadOnlyList<string> IncludeFiles { get; set; }
        public IReadOnlyList<string> ExcludeFiles { get; set; }

        public bool HandleIncludeExcludeFiles(JObject jsonObject)
        {
            var rawInclude = jsonObject["include"];
            var rawExclude = jsonObject["exclude"];
            var rawIncludeFiles = jsonObject["includeFiles"];
            var rawExcludeFiles = jsonObject["excludeFiles"];

            IEnumerable<string> include;
            IEnumerable<string> exclude;
            IEnumerable<string> includeFiles;
            IEnumerable<string> excludeFiles;
            bool foundOne = false;
            if (rawInclude != null && JsonPackageSpecReader.TryGetStringEnumerableFromJArray(rawInclude, out include))
            {
                Include = include.ToList();
                foundOne = true;
            }
            if (rawExclude != null && JsonPackageSpecReader.TryGetStringEnumerableFromJArray(rawExclude, out exclude))
            {
                Exclude = exclude.ToList();
                foundOne = true;
            }
            if (rawIncludeFiles != null && JsonPackageSpecReader.TryGetStringEnumerableFromJArray(rawIncludeFiles, out includeFiles))
            {
                IncludeFiles = includeFiles.ToList();
                foundOne = true;
            }
            if (rawExcludeFiles != null && JsonPackageSpecReader.TryGetStringEnumerableFromJArray(rawExcludeFiles, out excludeFiles))
            {
                ExcludeFiles = excludeFiles.ToList();
                foundOne = true;
            }

            return foundOne;
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCodeCombiner();

            hashCode.AddSequence(Include);
            hashCode.AddSequence(Exclude);
            hashCode.AddSequence(IncludeFiles);
            hashCode.AddSequence(ExcludeFiles);

            return hashCode.CombinedHash;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as IncludeExcludeFiles);
        }

        public bool Equals(IncludeExcludeFiles other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Include.SequenceEqualWithNullCheck(Include) &&
                   Exclude.SequenceEqualWithNullCheck(Exclude) &&
                   IncludeFiles.SequenceEqualWithNullCheck(IncludeFiles) &&
                   ExcludeFiles.SequenceEqualWithNullCheck(ExcludeFiles);
        }

        public IncludeExcludeFiles Clone()
        {
            var clonedObject = new IncludeExcludeFiles();
            clonedObject.Include = Include;
            clonedObject.Exclude = Exclude;
            clonedObject.IncludeFiles = IncludeFiles;
            clonedObject.ExcludeFiles = ExcludeFiles;
            return clonedObject;
        }
    }
}
