// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Frameworks;
using System;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// A data transfer type for calls to <see cref="EnvDTEProjectAdapter" />.
    /// </summary>
    public class LegacyCSProjPackageReference
    {
        public LegacyCSProjPackageReference(
            string name,
            string version,
            Array metadataElements,
            Array metadataValues,
            NuGetFramework targetNuGetFramework)
        {
            Name = name;
            Version = version;
            MetadataElements = metadataElements;
            MetadataValues = metadataValues;
            TargetNuGetFramework = targetNuGetFramework;
        }

        public string Name { get; private set; }
        public string Version  { get; private set; }
        public Array MetadataElements { get; private set; }
        public Array MetadataValues { get; private set; }
        public NuGetFramework TargetNuGetFramework { get; private set; }
    }
}
