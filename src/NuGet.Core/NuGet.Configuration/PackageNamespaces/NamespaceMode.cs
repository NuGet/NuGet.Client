// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Configuration
{
    public enum NamespaceMode
    {
        /// <summary>
        /// Relaxed mode for package namespaces.
        /// Any package id must be in one or more matching namespace declarations.
        /// </summary>
        MultipleSourcesPerPackage,

        /// <summary>
        /// Strict mode for package namespaces.
        /// No package id may match more than one feed.
        /// </summary>
        SingleSourcePerPackage
    }
}
