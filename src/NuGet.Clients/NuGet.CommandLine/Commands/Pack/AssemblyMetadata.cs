// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.CommandLine
{
    [Serializable]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public class AssemblyMetadata
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public AssemblyMetadata(Dictionary<string, string> properties = null)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            Properties = properties ??
                // Just like parameter replacements, these are also case insensitive, for consistency.
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string Name { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string Version { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string InformationalVersion { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string Title { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string Description { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string Company { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string Copyright { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        /// <summary>
        /// Supports extra metadata properties specified for an assembly 
        /// using AssemblyMetadataAttribute.
        /// </summary>
        public Dictionary<string, string> Properties { get; }
    }
}
