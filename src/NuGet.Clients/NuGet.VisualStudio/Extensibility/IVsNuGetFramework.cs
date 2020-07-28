// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.VisualStudio
{
    /// <summary>A type that represents the components of a .NET Target Framework Moniker.</summary>
    /// <remarks><c>System.Runtime.Versioning.FrameworkName</c> does not support .NET 5 Target Framework Monikers with a platform, but this type does.</remarks>
    public interface IVsNuGetFramework
    {
        /// <summary>The framework identifier.</summary>
        string TargetFrameworkIdentifier { get; }

        /// <summary>The framework version.</summary>
        string TargetFrameworkVersion { get; }

        /// <summary>The framework profile.</summary>
        /// <remarks>Has the value <c>String.Empty</c> if the framework does not use a profile.</remarks>
        string TargetFrameworkProfile { get; }

        /// <summary>The framework platform.</summary>
        /// <remarks>Has the value <c>String.Empty</c> if the framework does not use a profile.</remarks>
        string TargetPlatformIdentifier { get; }

        /// <summary>The framework platform version.</summary>
        /// <remarks>Has the value <c>String.Empty</c> if the framework does not use a profile.</remarks>
        string TargetPlatformVersion { get; }
    }
}
