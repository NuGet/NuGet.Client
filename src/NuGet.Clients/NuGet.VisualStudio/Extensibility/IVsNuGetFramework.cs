// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.InteropServices;

namespace NuGet.VisualStudio
{
    /// <summary>A type that represents the components of a .NET Target Framework Moniker.</summary>
    /// <remarks><see cref="System.Runtime.Versioning.FrameworkName"/> does not support .NET 5 Target Framework Monikers with a platform, but this type does.</remarks>
    [ComImport]
    [Guid("E57318D0-9A4D-443C-87F6-631D7F6B14CF")]
    public interface IVsNuGetFramework
    {
        /// <summary>The framework identifier.</summary>
        string TargetFrameworkIdentifier { get; }

        /// <summary>The framework version.</summary>
        string TargetFrameworkVersion { get; }

        /// <summary>The framework profile.</summary>
        string TargetFrameworkProfile { get; }

        /// <summary>The framework platform.</summary>
        string TargetPlatformIdentifier { get; }

        /// <summary>The framework platform version.</summary>
        string TargetPlatformVersion { get; }
    }
}
