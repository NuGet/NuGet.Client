// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.ProjectModel
{
    /// <summary>
    /// Represents the state of a build integrated project.
    /// </summary>
    public class DependencyGraphProjectCacheEntry
    {
        public DependencyGraphProjectCacheEntry(
            ISet<string> referenceClosure,
            DateTimeOffset? projectConfigLastModified)
        {
            if (referenceClosure == null)
            {
                throw new ArgumentNullException(nameof(referenceClosure));
            }
            
            ReferenceClosure = referenceClosure;
            ProjectConfigLastModified = projectConfigLastModified;
        }

        /// <summary>
        /// All project.json files and msbuild references in the closure.
        /// </summary>
        public ISet<string> ReferenceClosure { get; }

        /// <summary>
        /// Timestamp from the last time project.json was modified.
        /// </summary>
        public DateTimeOffset? ProjectConfigLastModified { get; }
    }
}
