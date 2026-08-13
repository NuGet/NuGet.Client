// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Test.Utility.Threading;
using Xunit;

namespace NuGetConsole.Host.PowerShell.Test
{
    [CollectionDefinition(CollectionName)]
    public class DispatcherThreadCollection : ICollectionFixture<DispatcherThreadFixture>
    {
        public const string CollectionName = nameof(DispatcherThreadCollection);
    }
}
