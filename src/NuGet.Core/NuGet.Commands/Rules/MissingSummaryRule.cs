using System;
using System.Collections.Generic;
using NuGet.Packaging;

namespace NuGet.Commands.Rules
{
    internal class MissingSummaryRule : IPackageRule
    {
        private const int ThresholdDescriptionLength = 300;

        public IEnumerable<PackageIssue> Validate(PackageBuilder builder)
        {
            if (builder.Description.Length > ThresholdDescriptionLength && String.IsNullOrEmpty(builder.Summary))
            {
                yield return new PackageIssue(
                    AnalysisResources.MissingSummaryTitle,
                    AnalysisResources.MissingSummaryDescription,
                    AnalysisResources.MissingSummarySolution);
            }
        }
    }
}