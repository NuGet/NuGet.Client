// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace NuGet.Packaging.Rules
{
    public static class PackageCreationRuleSet
    {
        private static readonly ReadOnlyCollection<IPackageRule> _rules = new ReadOnlyCollection<IPackageRule>(
            new IPackageRule[] {
                new InvalidFrameworkFolderRule(AnalysisResources.InvalidFrameworkWarning),
                new MisplacedAssemblyUnderLibRule(AnalysisResources.AssemblyDirectlyUnderLibWarning),
                new MisplacedAssemblyOutsideLibRule(AnalysisResources.AssemblyOutsideLibWarning),
                new MisplacedScriptFileRule(AnalysisResources.ScriptOutsideToolsWarning),
                new MisplacedTransformFileRule(AnalysisResources.MisplacedTransformFileWarning),
                new InitScriptNotUnderToolsRule(AnalysisResources.MisplacedInitScriptWarning),
                new WinRTNameIsObsoleteRule( AnalysisResources.WinRTObsoleteWarning),
                new DefaultManifestValuesRule(AnalysisResources.DefaultSpecValueWarning),
                new InvalidPlaceholderFileRule(AnalysisResources.InvalidFrameworkWarning),
                new LegacyVersionRule(AnalysisResources.LegacyVersionWarning),
                new InvalidPrereleaseDependencyRule(AnalysisResources.InvalidPrereleaseDependencyWarning),
                new UnspecifiedDependencyVersionRule(AnalysisResources.UnspecifiedDependencyVersionWarning),
                new UnrecognizedScriptFileRule(AnalysisResources.UnrecognizedScriptWarning)
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
