using System;
using System.Collections.Generic;

namespace NuGet.CommandLine.Rules
{
    internal class MissingSummaryRule : IPackageRule
    {
        private const int ThresholdDescriptionLength = 300;

        public IEnumerable<PackageIssue> Validate(IPackage package)
        {
            if (package.Description.Length > ThresholdDescriptionLength && String.IsNullOrEmpty(package.Summary))
            {
                yield return new PackageIssue(
                    AnalysisResources.MissingSummaryTitle,
                    AnalysisResources.MissingSummaryDescription,
                    AnalysisResources.MissingSummarySolution);
            }
        }
    }
}