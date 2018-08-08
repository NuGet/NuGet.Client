// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NuGet.Common;
using NuGet.Versioning;

namespace NuGet.Packaging.Rules
{
    public class DefaultManifestValuesRule : IPackageRule
    {
        internal static readonly string SampleProjectUrl = "http://PROJECT_URL_HERE_OR_DELETE_THIS_LINE/";
        internal static readonly string SampleLicenseUrl = "http://LICENSE_URL_HERE_OR_DELETE_THIS_LINE/";
        internal static readonly string SampleIconUrl = "http://ICON_URL_HERE_OR_DELETE_THIS_LINE/";
        internal static readonly string SampleTags = "Tag1 Tag2";
        internal static readonly string SampleReleaseNotes = "Summary of changes made in this release of the package.";
        internal static readonly string SampleDescription = "Package description";
        internal static readonly string SampleManifestDependencyId = "SampleDependency";
        internal static readonly string SampleManifestDependencyVersion = "1.0";

        public string MessageFormat { get; }

        public DefaultManifestValuesRule(string messageFormat)
        {
            MessageFormat = messageFormat;
        }

        public IEnumerable<PackagingLogMessage> Validate(PackageArchiveReader builder)
        {
            if(builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }
            var nuspecReader = builder.NuspecReader;
            if (SampleProjectUrl.Equals(nuspecReader.GetProjectUrl(), StringComparison.OrdinalIgnoreCase))
            {
                yield return CreateIssueFor("ProjectUrl", nuspecReader.GetProjectUrl());
            }
            if (SampleLicenseUrl.Equals(nuspecReader.GetLicenseUrl(), StringComparison.OrdinalIgnoreCase))
            {
                yield return CreateIssueFor("LicenseUrl", nuspecReader.GetLicenseUrl());
            }
            if (SampleIconUrl.Equals(nuspecReader.GetIconUrl(), StringComparison.OrdinalIgnoreCase))
            {
                yield return CreateIssueFor("IconUrl", nuspecReader.GetIconUrl());
            }
            if (SampleTags.Equals(nuspecReader.GetTags(), StringComparison.Ordinal))
            {
                yield return CreateIssueFor("Tags", SampleTags);
            }
            if (SampleReleaseNotes.Equals(nuspecReader.GetReleaseNotes(), StringComparison.Ordinal))
            {
                yield return CreateIssueFor("ReleaseNotes", SampleReleaseNotes);
            }
            if (SampleDescription.Equals(nuspecReader.GetDescription(), StringComparison.Ordinal))
            {
                yield return CreateIssueFor("Description", SampleDescription);
            }

            var dependency = nuspecReader.GetDependencyGroups().SelectMany(d => d.Packages).FirstOrDefault();
            if (dependency != null &&
                dependency.Id.Equals(SampleManifestDependencyId, StringComparison.OrdinalIgnoreCase) &&
                dependency.VersionRange != null &&
                dependency.VersionRange.ToString().Equals("[" + SampleManifestDependencyVersion + "]", StringComparison.OrdinalIgnoreCase))
            {
                yield return CreateIssueFor("Dependency", dependency.ToString());
            }
        }

        private PackagingLogMessage CreateIssueFor(string field, string value)
        {
            return PackagingLogMessage.CreateWarning(
                string.Format(CultureInfo.CurrentCulture, MessageFormat, value, field),
                NuGetLogCode.NU5102);
        }
    }
}
