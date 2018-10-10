// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace NuGet.Packaging.Rules
{
    public static class RuleSet
    {
        private static readonly ReadOnlyCollection<IPackageRule> _packageCreationRules = new ReadOnlyCollection<IPackageRule>(
            new IPackageRule[] {
                new InvalidFrameworkFolderRule(AnalysisResources.InvalidFrameworkWarning),
                new MisplacedAssemblyUnderLibRule(AnalysisResources.AssemblyDirectlyUnderLibWarning),
                new MisplacedAssemblyOutsideLibRule(AnalysisResources.AssemblyOutsideLibWarning),
                new MisplacedScriptFileRule(AnalysisResources.ScriptOutsideToolsWarning),
                new MisplacedTransformFileRule(AnalysisResources.MisplacedTransformFileWarning),
                new InitScriptNotUnderToolsRule(AnalysisResources.MisplacedInitScriptWarning),
                new WinRTNameIsObsoleteRule( AnalysisResources.WinRTObsoleteWarning),
                new DefaultManifestValuesRule(AnalysisResources.DefaultSpecValueWarning),
                new InvalidPlaceholderFileRule(AnalysisResources.InvalidPlaceholderFileWarning),
                new LegacyVersionRule(AnalysisResources.LegacyVersionWarning),
                new InvalidPrereleaseDependencyRule(AnalysisResources.InvalidPrereleaseDependencyWarning),
                new UnspecifiedDependencyVersionRule(AnalysisResources.UnspecifiedDependencyVersionWarning),
                new UnrecognizedScriptFileRule(AnalysisResources.UnrecognizedScriptWarning),
                new PathTooLongRule(AnalysisResources.FilePathTooLongWarning),
                new UnrecognizedLicenseIdentifierRule(AnalysisResources.UnrecognizedLicenseIdentifier),
                new LicenseUrlDeprecationWarning(AnalysisResources.LicenseUrlDeprecationWarning),
            }
        );

        private static readonly ReadOnlyCollection<IPackageRule> _packagesConfigToPackageReferenceMigrationRuleSet = new ReadOnlyCollection<IPackageRule>(
            new IPackageRule[] {
                new MisplacedAssemblyUnderLibRule(AnalysisResources.Migrator_AssemblyDirectlyUnderLibWarning),
                new InstallScriptInPackageReferenceProjectRule(AnalysisResources.Migrator_PackageHasInstallScript),
                new ContentFolderInPackageReferenceProjectRule(AnalysisResources.Migrator_PackageHasContentFolder),
                new XdtTransformInPackageReferenceProjectRule(AnalysisResources.Migrator_XdtTransformInPackage)
            }
        );

        public static IEnumerable<IPackageRule> PackageCreationRuleSet
        {
            get
            {
                return _packageCreationRules;
            }
        }

        public static IEnumerable<IPackageRule> PackagesConfigToPackageReferenceMigrationRuleSet
        {
            get
            {
                return _packagesConfigToPackageReferenceMigrationRuleSet;
            }
        }
    }
}
