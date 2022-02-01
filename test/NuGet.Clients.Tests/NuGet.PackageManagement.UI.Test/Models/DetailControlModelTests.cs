// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceHub.Framework;
using NuGet.VisualStudio.Internal.Contracts;
using Moq;
using Xunit;
using NuGet.Versioning;

namespace NuGet.PackageManagement.UI.Test
{
    public class DetailControlModelTests
    {
        [Theory]
        [InlineData("*", "ANewPackage")]
        [InlineData("*-*", "ANewPackage")]
        [InlineData("0.0.0", "ANewPackage")]
        [InlineData("[0.0.0,)", "ANewPackage")]
        [InlineData("(0.0.0,)", "ANewPackage > 0.0.0")]
        [InlineData("1.0.0", "ANewPackage >= 1.0.0")]
        public void DeprecationAlternativePackage_WithAsterisk_ShowsNoVersionInfo(string versionRange, string expected)
        {
            var model = new TestDetailControlModel(
                Mock.Of<IServiceBroker>(),
                Enumerable.Empty<IProjectContextInfo>());

            var metadata = new DetailedPackageMetadata()
            {
                DeprecationMetadata = new PackageDeprecationMetadataContextInfo(
                    message: "package deprecated",
                    reasons: new[] { "package deprecated", "legacy" },
                    alternatePackageContextInfo: new AlternatePackageMetadataContextInfo(
                         packageId: "ANewPackage",
                         range: VersionRange.Parse(versionRange))
                )
            };
            model.PackageMetadata = metadata;

            Assert.NotNull(model.PackageDeprecationAlternatePackageText);
            Assert.Equal(expected, model.PackageDeprecationAlternatePackageText);
        }
    }
}
