using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace NuGet.CommandLine
{
    public class DefaultManifestValuesRule : IPackageRule
    {
        public IEnumerable<PackageIssue> Validate(IPackage package)
        {
            if (package.ProjectUrl != null && package.ProjectUrl.OriginalString.Equals(SpecCommand.SampleProjectUrl, StringComparison.Ordinal))
            {
                yield return CreateIssueFor("ProjectUrl", package.ProjectUrl.OriginalString);
            }
            if (package.LicenseUrl != null && package.LicenseUrl.OriginalString.Equals(SpecCommand.SampleLicenseUrl, StringComparison.Ordinal))
            {
                yield return CreateIssueFor("LicenseUrl", package.LicenseUrl.OriginalString);
            }
            if (package.IconUrl != null && package.IconUrl.OriginalString.Equals(SpecCommand.SampleIconUrl, StringComparison.Ordinal))
            {
                yield return CreateIssueFor("IconUrl", package.IconUrl.OriginalString);
            }
            if (!String.IsNullOrEmpty(package.Tags) && package.Tags.Trim().Equals(SpecCommand.SampleTags, StringComparison.Ordinal))
            {
                yield return CreateIssueFor("Tags", SpecCommand.SampleTags);
            }
            if (SpecCommand.SampleReleaseNotes.Equals(package.ReleaseNotes, StringComparison.Ordinal))
            {
                yield return CreateIssueFor("ReleaseNotes", SpecCommand.SampleReleaseNotes);
            }
            if (SpecCommand.SampleDescription.Equals(package.Description, StringComparison.Ordinal))
            {
                yield return CreateIssueFor("Description", SpecCommand.SampleDescription);
            }

            var dependency = package.GetCompatiblePackageDependencies(targetFramework: null).FirstOrDefault();
            if (dependency != null &&
                dependency.Id.Equals(SpecCommand.SampleManifestDependency.Id, StringComparison.Ordinal) &&
                dependency.VersionSpec != null &&
                dependency.VersionSpec.ToString().Equals("[" + SpecCommand.SampleManifestDependency.Version + "]", StringComparison.Ordinal))
            {
                yield return CreateIssueFor("Dependency", dependency.ToString());
            }

            if (dependency != null && dependency.VersionSpec == null)
            {
                var message = String.Format(
                    CultureInfo.CurrentCulture,
                    LocalizedResourceManager.GetString("Warning_UnspecifiedDependencyVersion"),
                    dependency.Id);
                var issue = new PackageIssue(
                    LocalizedResourceManager.GetString("Warning_UnspecifiedDependencyVersionTitle"),
                    message,
                    LocalizedResourceManager.GetString("Warning_UnspecifiedDependencyVersionSolution"));
                yield return issue;
            }
        }

        private static PackageIssue CreateIssueFor(string field, string value)
        {
            return new PackageIssue(LocalizedResourceManager.GetString("Warning_DefaultSpecValueTitle"),
                String.Format(CultureInfo.CurrentCulture, LocalizedResourceManager.GetString("Warning_DefaultSpecValue"), value, field),
                LocalizedResourceManager.GetString("Warning_DefaultSpecValueSolution"));
        }
    }
}
