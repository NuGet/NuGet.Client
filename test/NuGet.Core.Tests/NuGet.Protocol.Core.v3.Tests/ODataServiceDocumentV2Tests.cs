using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;
using Test.Utility;
using Xunit;

namespace NuGet.Protocol.Core.v3.Tests
{
    public class ODataServiceDocumentV2Tests
    {
        [Fact]
        public async Task DefaultBaseAddressIsServiceAddress()
        {
            // Arrange
            var serviceAddress = TestUtility.CreateServiceAddress();

            var responses = new Dictionary<string, string>();
            responses.Add(serviceAddress, string.Empty);

            var repo = StaticHttpHandler.CreateSource(serviceAddress, Repository.Provider.GetCoreV3(), responses);

            var oDataServiceDocumentResource = await repo.GetResourceAsync<ODataServiceDocumentResourceV2>();

            // Act
            var baseAddress = oDataServiceDocumentResource.BaseAddress;

            // Assert
            Assert.Equal(serviceAddress.Trim('/'), baseAddress);
        }

        [Fact]
        public async Task BaseAddressExtractedFromServiceDocument()
        {
            // Arrange
            var serviceAddress = TestUtility.CreateServiceAddress();

            var responses = new Dictionary<string, string>();
            responses.Add(serviceAddress, TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.ODataServiceDocument.xml", GetType()));

            var repo = StaticHttpHandler.CreateSource(serviceAddress, Repository.Provider.GetCoreV3(), responses);

            var oDataServiceDocumentResource = await repo.GetResourceAsync<ODataServiceDocumentResourceV2>();

            // Act
            var baseAddress = oDataServiceDocumentResource.BaseAddress;

            // Assert
            Assert.Equal("https://bringing/it/all/back/home", baseAddress);
        }
    }
}
