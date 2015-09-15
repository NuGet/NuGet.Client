using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace NuGet.CommandLine
{
    [Export(typeof(IPackageRule))]
    internal sealed class DefaultPackageRules : IPackageRule
    {
        public IEnumerable<PackageIssue> Validate(IPackage package)
        {
            var commandLineRules = new IPackageRule[] { new DefaultManifestValuesRule() };
            return NuGet.CommandLine.Rules.DefaultPackageRuleSet.Rules
                .Concat(commandLineRules)
                .SelectMany(p => p.Validate(package));
        }
    }
}