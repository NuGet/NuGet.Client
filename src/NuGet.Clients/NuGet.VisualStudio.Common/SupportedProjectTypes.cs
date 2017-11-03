// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.VisualStudio;

namespace NuGet.VisualStudio
{
    public static class SupportedProjectTypes
    {
        private static readonly HashSet<string> _supportedProjectTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                VsProjectTypes.WebSiteProjectTypeGuid,
                VsProjectTypes.CsharpProjectTypeGuid,
                VsProjectTypes.VbProjectTypeGuid,
                VsProjectTypes.CppProjectTypeGuid,
                VsProjectTypes.JsProjectTypeGuid,
                VsProjectTypes.FsharpProjectTypeGuid,
                VsProjectTypes.NemerleProjectTypeGuid,
                VsProjectTypes.WixProjectTypeGuid,
                VsProjectTypes.SynergexProjectTypeGuid,
                VsProjectTypes.NomadForVisualStudioProjectTypeGuid,
                VsProjectTypes.TDSProjectTypeGuid,
                VsProjectTypes.DxJsProjectTypeGuid,
                VsProjectTypes.DeploymentProjectTypeGuid,
                VsProjectTypes.CosmosProjectTypeGuid,
                VsProjectTypes.ManagementPackProjectTypeGuid,
            };

        private static readonly HashSet<string> _unsupportedProjectTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                VsProjectTypes.LightSwitchProjectTypeGuid,
                VsProjectTypes.InstallShieldLimitedEditionTypeGuid,
            };

        // List of project types that cannot have binding redirects added
        private static readonly string[] _unsupportedProjectTypesForBindingRedirects =
            {
                VsProjectTypes.WixProjectTypeGuid,
                VsProjectTypes.JsProjectTypeGuid,
                VsProjectTypes.NemerleProjectTypeGuid,
                VsProjectTypes.CppProjectTypeGuid,
                VsProjectTypes.SynergexProjectTypeGuid,
                VsProjectTypes.NomadForVisualStudioProjectTypeGuid,
                VsProjectTypes.DxJsProjectTypeGuid,
                VsProjectTypes.CosmosProjectTypeGuid,
            };

        // List of project types that cannot have references added to them
        private static readonly string[] _unsupportedProjectTypesForAddingReferences =
            {
                VsProjectTypes.WixProjectTypeGuid,
                VsProjectTypes.CppProjectTypeGuid,
            };

        private static readonly HashSet<string> _unsupportedProjectExtension = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".metaproj",
            ".shproj",
            ".vcxitems",
            ".sqlproj"
        };

        public static bool IsSupported(string projectKind)
        {
            return _supportedProjectTypes.Contains(projectKind);
        }

        public static bool IsUnsupported(string projectKind)
        {
            return _unsupportedProjectTypes.Contains(projectKind);
        }

        public static bool IsSupportedForBindingRedirects(string projectKind)
        {
            return !_unsupportedProjectTypesForBindingRedirects.Contains(projectKind, StringComparer.OrdinalIgnoreCase);
        }

        public static bool IsSupportedForAddingReferences(string projectKind)
        {
            return !_unsupportedProjectTypesForAddingReferences.Contains(projectKind, StringComparer.OrdinalIgnoreCase);
        }

        public static bool IsSupportedProjectExtension(string projectPath)
        {
            return !_unsupportedProjectExtension.Contains(Path.GetExtension(projectPath));
        }
    }
}
