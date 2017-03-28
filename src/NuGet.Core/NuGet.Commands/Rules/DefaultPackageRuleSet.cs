// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace NuGet.Commands.Rules
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
                new WinRTNameIsObsoleteRule(),
                new DefaultManifestValuesRule(),
                new InvalidPlaceholderFileRule(),
                new LegacyVersionRule(),
                new InvalidPrereleaseDependencyRule()
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
