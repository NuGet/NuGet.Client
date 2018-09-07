// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Frameworks;
using System;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// A data transfer type for calls to <see cref="EnvDTEProjectAdapter" />.
    /// </summary>
    public class LegacyCSProjProjectReference
    {
        public LegacyCSProjProjectReference(string uniqueName, Array metadataElements, Array metadataValues)
        {
            UniqueName = uniqueName;
            MetadataElements = metadataElements;
            MetadataValues = metadataValues;
        }

        public string UniqueName { get; }
        public Array MetadataElements { get; }
        public Array MetadataValues { get; }
    }
}
