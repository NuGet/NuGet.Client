// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Common;
using NuGet.Shared;

namespace NuGet.ProjectModel
{
    public class CacheFile : IEquatable<CacheFile>
    {
        internal const int CurrentVersion = 2;

        public string DgSpecHash { get;}

        public int Version { get; set; }

        public bool Success { get; set; }

        public IList<string> ExpectedFiles { get; set; }

        public bool AnyPackagesMissing { get; set; }

        public string ProjectFullPath { get; set; }

        public IList<IAssetsLogMessage> LogMessages { get; set; }

        public bool IsValid { get { return Version == CurrentVersion && Success && DgSpecHash != null;  } }

        public CacheFile(string dgSpecHash)
        {
            DgSpecHash = dgSpecHash;
            Version = CurrentVersion;
        }

        public bool Equals(CacheFile other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Version == other.Version && Success == other.Success && StringComparer.Ordinal.Equals(DgSpecHash, other.DgSpecHash) && PathUtility.GetStringComparerBasedOnOS().Equals(ProjectFullPath, other.ProjectFullPath);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as CacheFile);
        }

        public override int GetHashCode()
        {
            var combiner = new HashCodeCombiner();
            combiner.AddObject(DgSpecHash);
            combiner.AddObject(Version);
            combiner.AddObject(ProjectFullPath);
            return combiner.CombinedHash;
        }
    }
}
