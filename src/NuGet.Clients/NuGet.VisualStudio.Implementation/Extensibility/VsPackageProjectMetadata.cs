// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.ProjectManagement;

namespace NuGet.VisualStudio
{
    internal class VsPackageProjectMetadata : IVsPackageProjectMetadata
    {
        public VsPackageProjectMetadata() : this(string.Empty, string.Empty)
        { }

        public VsPackageProjectMetadata(string id, string name)
        {
            BatchId = id ?? string.Empty;
            ProjectName = name ?? string.Empty;
        }

        public string BatchId { get; }

        public string ProjectName { get; }
    }
}
