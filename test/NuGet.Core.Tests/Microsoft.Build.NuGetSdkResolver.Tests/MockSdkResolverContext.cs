// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Build.Framework;

namespace Microsoft.Build.NuGetSdkResolver.Test
{
    /// <summary>
    /// A mock implementation of <see cref="SdkResolverContext"/> that uses a <see cref="MockSdkLogger"/>.
    /// </summary>
    public sealed class MockSdkResolverContext : SdkResolverContext
    {
        /// <summary>
        /// Initializes a new instance of the MockSdkResolverContext class.
        /// </summary>
        /// <param name="projectPath">The path to the project.</param>
        public MockSdkResolverContext(string projectPath)
        {
            Logger = MockSdkLogger;

            ProjectFilePath = projectPath;
        }

        /// <summary>
        /// Gets the <see cref="MockSdkLogger"/> being used by the context.
        /// </summary>
        public MockSdkLogger MockSdkLogger { get; } = new MockSdkLogger();
    }
}
