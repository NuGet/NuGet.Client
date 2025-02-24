// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable
using System;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.PackageManagement.UI
{
    public abstract class PackageModel
    {
        protected PackageModel(PackageIdentity identity,
            string? title = null,
            string? description = null,
            string? authors = null,
            Uri? projectUrl = null,
            string[]? tags = null,
            string? copyright = null)
        {
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            Title = title;
            Description = description;
            Authors = authors;
            ProjectUrl = projectUrl;
            Tags = tags;
            Copyright = copyright;
        }

        public PackageIdentity Identity { get; }

        public string Id => Identity.Id;

        public NuGetVersion Version => Identity.Version;

        public string? Title { get; }

        public string? Description { get; }

        public string? Authors { get; }

        public Uri? ProjectUrl { get; }

        public string[]? Tags { get; }

        public string? Copyright { get; }
    }
}
