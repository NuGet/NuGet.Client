using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.CommandLine.Rules
{
    public static class DefaultPackageRuleSet
    {
        private static readonly ReadOnlyCollection<IPackageRule> _rules = new ReadOnlyCollection<IPackageRule>(
            new IPackageRule[] {
                new InvalidFrameworkFolderRule(),
                new MisplacedAssemblyRule(),
                new MisplacedScriptFileRule(),
                new MisplacedTransformFileRule(),
                new MissingSummaryRule(),
                new InitScriptNotUnderToolsRule(),
                new WinRTNameIsObsoleteRule()
            }
        );

        public static IEnumerable<IPackageRule> Rules
        {
            get
            {
                return _rules;
            }
        }
    }
}
