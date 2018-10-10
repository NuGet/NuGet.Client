// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Packaging;
using NuGet.Packaging.Licenses;
using Xunit;

namespace NuGet.PackageManagement.UI.Test
{
    public class PackageLicenseUtilitiesTests
    {
        // TODO NK - Add warnings for the scenarios in which a license is unknown.
        [Theory]
        [InlineData("MIT", 1, new string[] { "MIT" })]
        [InlineData("(MIT)", 3, new string[] { "MIT" })]
        [InlineData("MIT OR Apache-2.0", 3, new string[] { "MIT", "Apache-2.0" })]
        [InlineData("(MIT OR Apache-2.0)", 5, new string[] { "MIT", "Apache-2.0" })]
        [InlineData("(MIT OR RandomLicenseRefStillGetsLinkGenerated)", 5, new string[] { "MIT", "RandomLicenseRefStillGetsLinkGenerated" })]
        [InlineData("((MIT WITH 389-exception) AND Apache-2.0)", 7, new string[] { "MIT", "389-exception", "Apache-2.0" })]
        [InlineData("((( (AMDPLPA) AND BSD-2-Clause)))", 5, new string[] { "AMDPLPA", "BSD-2-Clause" })]
        public void PackageLicenseUtility_GeneratesBasicLink(string license, int partsCount, string[] linkedText)
        {
            // Setup
            var expression = NuGetLicenseExpression.Parse(license);
            var licenseData = new LicenseMetadata(LicenseType.Expression, license, expression, null, LicenseMetadata.EmptyVersion);

            // Act
            var links = PackageLicenseUtilities.GenerateLicenseLinks(licenseData);

            // Assert
            Assert.Equal(partsCount, links.Count);

            var partsWithLinks = new List<LicenseText>();
            foreach (var part in links)
            {
                if (part is LicenseText licenseText)
                {
                    partsWithLinks.Add(licenseText);
                }
            }
            Assert.Equal(linkedText.Count(), partsWithLinks.Count);
            for (var i = 0; i < partsWithLinks.Count; i++)
            {
                Assert.Equal(linkedText[i], partsWithLinks[i].Text);
            }
            Assert.Equal(license, string.Join("", links.Select(e => e.Text)));
        }

        [Fact]
        public void PackageLicenseUtility_GeneratesLinkWithHigherVersion()
        {
            var license = "Not so random unparsed license";
            // Setup
            var licenseData = new LicenseMetadata(LicenseType.Expression, license, null, null, new System.Version(LicenseMetadata.CurrentVersion.Major + 1, 0, 0));

            // Act
            // TODO NK - The link generated here should contain a warning icon.
            var links = PackageLicenseUtilities.GenerateLicenseLinks(licenseData);
        }
    }
}
