// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGet.VisualStudio.Common.Test
{
    [CollectionDefinition(nameof(TestJoinableTaskFactoryCollection))]
    public class TestJoinableTaskFactoryCollection : ICollectionFixture<TestJoinableTaskFactory>
    {
    }
}
