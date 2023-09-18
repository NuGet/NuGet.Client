// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Frameworks;

namespace NuGet.Client
{
    /// <summary>
    /// An internal NuGetFramework marker for ManagedCodeConventions.
    /// Most conventions disallow the string 'any' as a txm, so to allow
    /// it for conventions with no txm in the path we use this special type.
    /// </summary>
#pragma warning disable RS0016 // Add public types and members to the declared API
    public class AnyFramework : NuGetFramework
    {
        public static AnyFramework Instance { get; } = new AnyFramework();

        private AnyFramework()
            : base(NuGetFramework.AnyFramework)
        {
        }
    }
}
