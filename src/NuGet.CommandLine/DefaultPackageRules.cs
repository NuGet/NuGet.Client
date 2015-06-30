//using System.Collections.Generic;
//using System.ComponentModel.Composition;
//using System.Linq;

//namespace NuGet
//{
//    [Export(typeof(IPackageRule))]
//    internal sealed class DefaultPackageRules : IPackageRule
//    {
//        public IEnumerable<PackageIssue> Validate(IPackage package)
//        {
//            var commandLineRules = new IPackageRule[] { new DefaultManifestValuesRule() };
//            return DefaultPackageRuleSet.Rules
//                                        .Concat(commandLineRules)
//                                        .SelectMany(p => p.Validate(package));
//        }
//    }
//}