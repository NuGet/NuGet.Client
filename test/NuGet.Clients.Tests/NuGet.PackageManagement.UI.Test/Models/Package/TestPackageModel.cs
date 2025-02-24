// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable
using System;
using NuGet.Packaging.Core;

namespace NuGet.PackageManagement.UI.Test.Models
{
    internal class TestPackageModel : PackageModel
    {
        public TestPackageModel(PackageIdentity identity,
            string? title = null,
            string? description = null,
            string? authors = null,
            Uri? projectUrl = null,
            string[]? tags = null,
            string? copyright = null)
            : base(identity, title, description, authors, projectUrl, tags, copyright)
        {
        }
    }
}
