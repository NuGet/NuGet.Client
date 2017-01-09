﻿using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using NuGet.Packaging;

namespace NuGet.Commands.Rules
{
    /// <summary>
    /// Warn if the version is not parsable by older nuget clients.
    /// </summary>
    /// <remarks>This rule should be removed once more users move to SemVer 2.0.0 capable clients.</remarks>
    internal class LegacyVersionRule : IPackageRule
    {
        // NuGet 2.12 regex for version parsing.
        private const string LegacyRegex = @"^(?<Version>\d+(\s*\.\s*\d+){0,3})(?<Release>-[a-z][0-9a-z-]*)?$";

        public IEnumerable<PackageIssue> Validate(PackageBuilder builder)
        {
            var regex = new Regex(LegacyRegex, RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);

            if (!regex.IsMatch(builder.Version.ToFullString()))
            {
                yield return new PackageIssue(
                    AnalysisResources.LegacyVersionTitle,
                    string.Format(CultureInfo.CurrentCulture, AnalysisResources.LegacyVersionDescription, builder.Version.ToFullString()),
                    AnalysisResources.LegacyVersionSolution);
            }
        }
    }
}