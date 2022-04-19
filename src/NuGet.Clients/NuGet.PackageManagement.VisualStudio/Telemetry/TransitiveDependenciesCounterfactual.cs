// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// This class avoids generic type issues and static variables in <see cref="PackageReferenceProject{T, U}" />
    /// Also, it keeps all counterfactual state in one place
    /// </summary>
    internal static class TransitiveDependenciesCounterfactual
    {
        internal static int EmittedFlag = 0;
        internal static int PMUIEmittedFlag = 0;

        /// <summary>
        /// Indicates if counterfactual telemetry for Transitive Dependencies should be emitted.
        /// </summary>
        public static bool ShouldEmitTelemetry => Interlocked.CompareExchange(ref EmittedFlag, 1, 0) == 0;

        /// <summary>
        /// Indicates if counterfactual telemetry for PM UI Transitive Dependencies should be emitted.
        /// </summary>
        public static bool ShouldEmitPMUITelemetry => Interlocked.CompareExchange(ref PMUIEmittedFlag, 1, 0) == 0;
    }
}
