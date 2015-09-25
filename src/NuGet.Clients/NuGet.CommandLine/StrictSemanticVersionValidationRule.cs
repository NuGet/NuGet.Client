using System;
using System.Collections.Generic;
using System.Globalization;

namespace NuGet
{
    public class StrictSemanticVersionValidationRule : IPackageRule
    {
        public IEnumerable<PackageIssue> Validate(IPackage package)
        {
            SemanticVersion semVer;
            if (!SemanticVersion.TryParseStrict(package.Version.ToString(), out semVer))
            {
                yield return new PackageIssue(LocalizedResourceManager.GetString("Warning_SemanticVersionTitle"),
                    String.Format(CultureInfo.CurrentCulture, LocalizedResourceManager.GetString("Warning_SemanticVersion"), package.Version),
                    LocalizedResourceManager.GetString("Warning_SemanticVersionSolution"));
            }
        }
    }
}
