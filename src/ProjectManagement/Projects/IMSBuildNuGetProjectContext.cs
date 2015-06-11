// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.ProjectManagement
{
    public interface IMSBuildNuGetProjectContext : INuGetProjectContext
    {
        bool SkipAssemblyReferences { get; }
        bool BindingRedirectsDisabled { get; }

        /// <summary>
        /// Gets or sets a value that determines if adding binding redirects can be skipped
        /// as a post-install operation. This is complementary to <see cref="BindingRedirectsDisabled"/>
        /// and is set when a package does not have any "lib" items.
        /// </summary>
        bool SkipBindingRedirects { get; set; }
    }
}
