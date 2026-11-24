// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace NuGet.Protocol
{
    public sealed class PackageStagingGroupReference
    {
        [JsonConstructor]
        internal PackageStagingGroupReference(string id, string? name = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new JsonException();
            }

            Id = id;
            Name = name;
        }

        public string Id { get; }

        public string? Name { get; }
    }
}
