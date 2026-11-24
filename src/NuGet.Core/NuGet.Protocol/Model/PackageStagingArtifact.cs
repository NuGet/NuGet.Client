// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NuGet.Protocol
{
    /// <summary>
    /// Describes a staged package or symbols artifact.
    /// </summary>
    public sealed class PackageStagingArtifact
    {
        [JsonConstructor]
        internal PackageStagingArtifact(
            string id,
            string version,
            string kind,
            string status,
            PackageStagingGroupReference? group,
            bool canPromote,
            string? owner = null,
            string? uploaded = null,
            string? expires = null,
            bool? listed = null,
            string? managementUrl = null,
            IReadOnlyList<PackageStagingPromotionBlocker>? blockers = null)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(version)
                || string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(status))
            {
                throw new JsonException();
            }

            var promotionBlockers = blockers is null ? null : new List<PackageStagingPromotionBlocker>(blockers);
            promotionBlockers?.RemoveAll(static blocker => blocker is null);

            Id = id;
            Version = version;
            Kind = kind;
            Status = status;
            Group = group;
            CanPromote = canPromote;
            Owner = owner;
            Uploaded = uploaded;
            Expires = expires;
            Listed = listed;
            ManagementUrl = managementUrl;
            Blockers = promotionBlockers?.AsReadOnly();
        }

        public string Id { get; }
        public string Version { get; }
        public string Kind { get; }
        public string Status { get; }
        public PackageStagingGroupReference? Group { get; }
        public bool CanPromote { get; }
        public string? Owner { get; }
        public string? Uploaded { get; }
        public string? Expires { get; }
        public bool? Listed { get; }
        public string? ManagementUrl { get; }
        public IReadOnlyList<PackageStagingPromotionBlocker>? Blockers { get; }
    }
}
