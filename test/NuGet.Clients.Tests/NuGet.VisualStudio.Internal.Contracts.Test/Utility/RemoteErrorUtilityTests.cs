// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit;

namespace NuGet.VisualStudio.Internal.Contracts.Test
{
    public class RemoteErrorUtilityTests
    {
        [Fact]
        public void ToRemoteError_WhenExceptionIsNull_Throws()
        {
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
                () => RemoteErrorUtility.ToRemoteError(exception: null));

            Assert.Equal("exception", exception.ParamName);
        }
    }
}
