// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace Dotnet.Integration.Test
{
    [CollectionDefinition(nameof(DotnetIntegrationNotThreadSafeCollection), DisableParallelization = true)]
    public class DotnetIntegrationNotThreadSafeCollection : ICollectionFixture<MsbuildIntegrationTestFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }
}
