// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace NuGet.Protocol.Plugins.Tests
{
    public class GetOperationClaimsRequestTests
    {
        private static readonly string _packageSourceRepository = "A";
        private static readonly A _serviceIndex = new A() { B = "C" };
        private static readonly JObject _serviceIndexJson = JObject.FromObject(_serviceIndex);

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Constructor_ThrowsForNullOrEmptyPackageSourceRepository(string packageSourceRepository)
        {
            var exception = Assert.Throws<ArgumentException>(
                () => new GetOperationClaimsRequest(packageSourceRepository, _serviceIndexJson));

            Assert.Equal("packageSourceRepository", exception.ParamName);
        }

        [Fact]
        public void Constructor_ThrowsForNullServiceIndex()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new GetOperationClaimsRequest(_packageSourceRepository, serviceIndex: null));

            Assert.Equal("serviceIndex", exception.ParamName);
        }

        [Fact]
        public void Constructor_InitializesProperties()
        {
            var request = new GetOperationClaimsRequest(_packageSourceRepository, _serviceIndexJson);

            Assert.Equal(_packageSourceRepository, request.PackageSourceRepository);
            Assert.Equal("{\"B\":\"C\"}", request.ServiceIndex.ToString(Formatting.None));
        }

        [Fact]
        public void JsonSerialization_ReturnsCorrectJson()
        {
            var request = new GetOperationClaimsRequest(_packageSourceRepository, _serviceIndexJson);

            var json = TestUtilities.Serialize(request);

            Assert.Equal("{\"PackageSourceRepository\":\"A\",\"ServiceIndex\":{\"B\":\"C\"}}", json);
        }

        [Theory]
        [InlineData("{\"PackageSourceRepository\":\"A\",\"ServiceIndex\":{}}", "{}")]
        [InlineData("{\"PackageSourceRepository\":\"A\",\"ServiceIndex\":{\"B\":\"C\"}}", "{\"B\":\"C\"}")]
        public void JsonDeserialization_ReturnsCorrectObject(string json, string serviceIndex)
        {
            var request = JsonSerializationUtilities.Deserialize<GetOperationClaimsRequest>(json);

            Assert.Equal("A", request.PackageSourceRepository);
            Assert.Equal(serviceIndex, request.ServiceIndex.ToString(Formatting.None));
        }


        [Theory]
        [InlineData("{\"ServiceIndex\":{}}")]
        [InlineData("{\"PackageSourceRepository\":null,\"ServiceIndex\":{}}")]
        [InlineData("{\"PackageSourceRepository\":\"\",\"ServiceIndex\":{}}")]
        public void JsonDeserialization_ThrowsForInvalidPackageSourceRepository(string json)
        {
            var exception = Assert.Throws<ArgumentException>(
                () => JsonSerializationUtilities.Deserialize<GetOperationClaimsRequest>(json));

            Assert.Equal("packageSourceRepository", exception.ParamName);
        }

        [Theory]
        [InlineData("{\"PackageSourceRepository\":\"A\"}")]
        [InlineData("{\"PackageSourceRepository\":\"A\",\"ServiceIndex\":null}")]
        public void JsonDeserialization_ThrowsForInvalidServiceIndex(string json)
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => JsonSerializationUtilities.Deserialize<GetOperationClaimsRequest>(json));

            Assert.Equal("serviceIndex", exception.ParamName);
        }

        [Theory]
        [InlineData("{\"ServiceIndex\":\"\"}")]
        [InlineData("{\"ServiceIndex\":3}")]
        public void JsonDeserialization_ThrowsForInvalidServiceIndexValue(string json)
        {
            Assert.Throws<InvalidCastException>(
                () => JsonSerializationUtilities.Deserialize<GetOperationClaimsRequest>(json));
        }

        private sealed class A
        {
            public string B { get; set; }
        }
    }
}