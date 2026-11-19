// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Globalization;
using Xunit;

namespace NuGet.PackageManagement.UI.Test.Utility
{
    public class UIUtilityTests
    {
        [Fact]
        public void GetInstalledPackagesWarningText_NoVulnerableOrDeprecated_ReturnsNull()
        {
            Assert.Null(UIUtility.GetInstalledPackagesWarningText(vulnerablePackagesCount: 0, deprecatedPackagesCount: 0));
        }

        [Fact]
        public void GetInstalledPackagesWarningText_OneVulnerable_UsesSingularText()
        {
            string? text = UIUtility.GetInstalledPackagesWarningText(vulnerablePackagesCount: 1, deprecatedPackagesCount: 0);

            Assert.Equal(Resources.Label_Installed_VulnerableWarning_Single, text);
        }

        [Fact]
        public void GetInstalledPackagesWarningText_MultipleVulnerable_UsesPluralText()
        {
            string? text = UIUtility.GetInstalledPackagesWarningText(vulnerablePackagesCount: 3, deprecatedPackagesCount: 0);

            Assert.Equal(string.Format(CultureInfo.CurrentCulture, Resources.Label_Installed_VulnerableWarning, 3), text);
        }

        [Fact]
        public void GetInstalledPackagesWarningText_OneDeprecated_UsesSingularText()
        {
            string? text = UIUtility.GetInstalledPackagesWarningText(vulnerablePackagesCount: 0, deprecatedPackagesCount: 1);

            Assert.Equal(Resources.Label_Installed_DeprecatedWarning_Single, text);
        }

        [Fact]
        public void GetInstalledPackagesWarningText_MultipleDeprecated_UsesPluralText()
        {
            string? text = UIUtility.GetInstalledPackagesWarningText(vulnerablePackagesCount: 0, deprecatedPackagesCount: 4);

            Assert.Equal(string.Format(CultureInfo.CurrentCulture, Resources.Label_Installed_DeprecatedWarning, 4), text);
        }

        [Fact]
        public void GetInstalledPackagesWarningText_OneVulnerableAndOneDeprecated_UsesSingularTextForBoth()
        {
            string? text = UIUtility.GetInstalledPackagesWarningText(vulnerablePackagesCount: 1, deprecatedPackagesCount: 1);

            Assert.Equal(Resources.Label_Installed_VulnerableAndDeprecatedWarning_SingleBoth, text);
        }

        [Fact]
        public void GetInstalledPackagesWarningText_OneVulnerableAndMultipleDeprecated_UsesSingularVulnerableText()
        {
            string? text = UIUtility.GetInstalledPackagesWarningText(vulnerablePackagesCount: 1, deprecatedPackagesCount: 2);

            Assert.Equal(string.Format(CultureInfo.CurrentCulture, Resources.Label_Installed_VulnerableAndDeprecatedWarning_SingleVulnerable, 2), text);
        }

        [Fact]
        public void GetInstalledPackagesWarningText_MultipleVulnerableAndOneDeprecated_UsesSingularDeprecatedText()
        {
            string? text = UIUtility.GetInstalledPackagesWarningText(vulnerablePackagesCount: 2, deprecatedPackagesCount: 1);

            Assert.Equal(string.Format(CultureInfo.CurrentCulture, Resources.Label_Installed_VulnerableAndDeprecatedWarning_SingleDeprecated, 2), text);
        }

        [Fact]
        public void GetInstalledPackagesWarningText_MultipleVulnerableAndMultipleDeprecated_UsesPluralText()
        {
            string? text = UIUtility.GetInstalledPackagesWarningText(vulnerablePackagesCount: 2, deprecatedPackagesCount: 5);

            Assert.Equal(string.Format(CultureInfo.CurrentCulture, Resources.Label_Installed_VulnerableAndDeprecatedWarning, 2, 5), text);
        }
    }
}
