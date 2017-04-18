// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Common;
using Xunit;

namespace NuGet.Protocol.Plugins.Tests
{
    public class EmbeddedSignatureVerifierTests
    {
        [Fact]
        public void CreateOrNull_ReturnsPlatformAppropriateResult()
        {
            var verifier = EmbeddedSignatureVerifier.CreateOrNull();
            var expectedIsNull = !RuntimeEnvironmentHelper.IsWindows;
            var actualIsNull = verifier == null;

            Assert.Equal(expectedIsNull, actualIsNull);
        }
    }
}