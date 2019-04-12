// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text.RegularExpressions;
using Xunit;

namespace NuGet.Protocol.Tests
{
    public class ServiceTypesTests
    {
        [Fact]
        public void RepositorySignatures_AllResourcesAreVersioned()
        {
            var pattern = new Regex(@"^RepositorySignatures/[4-9]\.\d\.\d$");

            foreach (var repositorySignaturesResource in ServiceTypes.RepositorySignatures)
            {
                Assert.True(pattern.IsMatch(repositorySignaturesResource), $"RepositorySignatures resource '{repositorySignaturesResource}' is not versioned with an expected format.  Verify that {nameof(RepositorySignatureResourceProvider)} can handle any new formats.");
            }
        }
    }
}