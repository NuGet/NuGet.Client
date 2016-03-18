using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NuGet.Packaging;
using NuGet.Versioning;

namespace NuGet.Commands.Rules
{
    public class DefaultManifestValuesRule : IPackageRule
    {
        internal static readonly string SampleProjectUrl = "http://PROJECT_URL_HERE_OR_DELETE_THIS_LINE";
        internal static readonly string SampleLicenseUrl = "http://LICENSE_URL_HERE_OR_DELETE_THIS_LINE";
        internal static readonly string SampleIconUrl = "http://ICON_URL_HERE_OR_DELETE_THIS_LINE";
        internal static readonly string SampleTags = "Tag1 Tag2";
        internal static readonly string SampleReleaseNotes = "Summary of changes made in this release of the package.";
        internal static readonly string SampleDescription = "Package description";
        internal static readonly string SampleManifestDependencyId = "SampleDependency";
        internal static readonly string SampleManifestDependencyVersion = "1.0";

        public IEnumerable<PackageIssue> Validate(PackageBuilder builder)
        {
            if (builder.ProjectUrl != null && builder.ProjectUrl.OriginalString.Equals(SampleProjectUrl, StringComparison.Ordinal))
            {
                yield return CreateIssueFor("ProjectUrl", builder.ProjectUrl.OriginalString);
            }
            if (builder.LicenseUrl != null && builder.LicenseUrl.OriginalString.Equals(SampleLicenseUrl, StringComparison.Ordinal))
            {
                yield return CreateIssueFor("LicenseUrl", builder.LicenseUrl.OriginalString);
            }
            if (builder.IconUrl != null && builder.IconUrl.OriginalString.Equals(SampleIconUrl, StringComparison.Ordinal))
            {
                yield return CreateIssueFor("IconUrl", builder.IconUrl.OriginalString);
            }
            if (builder.Tags.Count() == 2 && string.Join(" ", builder.Tags).Equals(SampleTags))
            {
                yield return CreateIssueFor("Tags", SampleTags);
            }
            if (SampleReleaseNotes.Equals(builder.ReleaseNotes, StringComparison.Ordinal))
            {
                yield return CreateIssueFor("ReleaseNotes", SampleReleaseNotes);
            }
            if (SampleDescription.Equals(builder.Description, StringComparison.Ordinal))
            {
                yield return CreateIssueFor("Description", SampleDescription);
            }

            var dependency = builder.DependencyGroups.SelectMany(d => d.Packages).FirstOrDefault();
            if (dependency != null &&
                dependency.Id.Equals(SampleManifestDependencyId, StringComparison.Ordinal) &&
                dependency.VersionRange != null &&
                dependency.VersionRange.ToString().Equals("[" + SampleManifestDependencyVersion + "]", StringComparison.Ordinal))
            {
                yield return CreateIssueFor("Dependency", dependency.ToString());
            }

            if (dependency != null && dependency.VersionRange == VersionRange.All)
            {
                var message = String.Format(
                    CultureInfo.CurrentCulture,
                    AnalysisResources.UnspecifiedDependencyVersion,
                    dependency.Id);
                var issue = new PackageIssue(
                    AnalysisResources.UnspecifiedDependencyVersionTitle,
                    message,
                    AnalysisResources.UnspecifiedDependencyVersionSolution);
                yield return issue;
            }
        }

        private static PackageIssue CreateIssueFor(string field, string value)
        {
            return new PackageIssue(AnalysisResources.DefaultSpecValueTitle,
                String.Format(CultureInfo.CurrentCulture, AnalysisResources.DefaultSpecValue, value, field),
                AnalysisResources.DefaultSpecValueSolution);
        }
    }
}
