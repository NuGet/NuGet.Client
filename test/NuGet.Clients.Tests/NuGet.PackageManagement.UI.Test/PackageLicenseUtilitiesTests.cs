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
        [Theory]
        [InlineData("MIT", 1, new string[] { "MIT" }, false)]
        [InlineData("(MIT)", 3, new string[] { "MIT" }, false)]
        [InlineData("MIT OR Apache-2.0", 3, new string[] { "MIT", "Apache-2.0" }, false)]
        [InlineData("(MIT OR Apache-2.0)", 5, new string[] { "MIT", "Apache-2.0" }, false)]
        [InlineData("(MIT OR RandomLicenseRefStillGetsLinkGenerated)", 6, new string[] { "MIT", "RandomLicenseRefStillGetsLinkGenerated" }, true)]
        [InlineData("((MIT WITH 389-exception) AND Apache-2.0)", 7, new string[] { "MIT", "389-exception", "Apache-2.0" }, false)]
        [InlineData("((( (AMDPLPA) AND BSD-2-Clause)))", 5, new string[] { "AMDPLPA", "BSD-2-Clause" }, false)]
        public void PackageLicenseUtility_GeneratesBasicLink(string license, int partsCount, string[] linkedText, bool hasWarnings)
        {
            // Setup
            var expression = NuGetLicenseExpression.Parse(license);
            IList<string> warnings = null;
            if (hasWarnings)
            {
                warnings = new List<string> { "Random warning" };
            }

            var licenseData = new LicenseMetadata(LicenseType.Expression, license, expression, warnings, LicenseMetadata.EmptyVersion);

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
            Assert.Equal(license, string.Join("", links.Where(e => !(e is WarningText)).Select(e => e.Text)));
            if (hasWarnings)
            {
                Assert.NotNull(links[links.Count - 1] as WarningText);
            }
        }

        [Fact]
        public void PackageLicenseUtility_GeneratesLinkWithHigherVersion()
        {
            var license = "Not so random unparsed license";
            // Setup
            var licenseData = new LicenseMetadata(LicenseType.Expression, license, null, new List<string> { "bad license warning"}, new System.Version(LicenseMetadata.CurrentVersion.Major + 1, 0, 0));

            // Act
            var links = PackageLicenseUtilities.GenerateLicenseLinks(licenseData);

            Assert.True(links[links.Count - 1] is WarningText);
            Assert.Empty(links.Where(e => e is LicenseText));
        }

        [Fact]
        public void PackageLicenseUtility_GeneratesLinkForFiles()
        {
            // Setup
            var licenseData = new LicenseMetadata(LicenseType.File, "License.txt", null, null, LicenseMetadata.CurrentVersion);

            // Act
            var links = PackageLicenseUtilities.GenerateLicenseLinks(licenseData);

            Assert.Equal(1, links.Count);
            Assert.True(links[0] is FreeText);
            Assert.Equal(string.Format(Resources.License_FileEmbeddedInPackage, "License.txt"), links[0].Text);
            Assert.Empty(links.Where(e => e is LicenseText));
        }
    }
}
