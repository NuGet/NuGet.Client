// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Common.Test
{
    public class NetworkProtocolUtilityTests
    {
        private const string Ssl20Name = "SSLv2.0";
        private const string Ssl20TestUrl = "https://www.ssllabs.com:10200/";
        private const string Ssl30Name = "SSLv3.0";
        private const string Ssl30TestUrl = "https://www.ssllabs.com:10300/";
        private const string Tls10Name = "TLSv1.0";
        private const string Tls10TestUrl = "https://www.ssllabs.com:10301/";
        private const string Tls11Name = "TLSv1.1";
        private const string Tls11TestUrl = "https://www.ssllabs.com:10302/";
        private const string Tls12Name = "TLSv1.2";
        private const string Tls12TestUrl = "https://www.ssllabs.com:10303/";

        [ConditionalFact(typeof(MacOSRuntimeCondition))]
        public async Task NetworkProtocolUtility_NotSupportedProtocol()
        {
            var url = Ssl20TestUrl;

            // Arrange
            NetworkProtocolUtility.ConfigureSupportedSslProtocols();
            var client = new HttpClient();

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync(url));
        }

        [ConditionalFact(typeof(MacOSRuntimeCondition))]
        public async Task NetworkProtocolUtility_PlatformSpecific()
        {
            var url = Ssl30TestUrl;

            NetworkProtocolUtility.ConfigureSupportedSslProtocols();
            var client = new HttpClient();

#if DNXCORE50
            // .NET Core bug: https://github.com/dotnet/corefx/issues/6668
            var supported = RuntimeEnvironmentHelper.IsLinux;
#else
            var supported = false;
#endif

            if (supported)
            {
                // Act
                using (var response = await client.GetAsync(url))
                {
                    // Assert
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                }
            }
            else
            {
                // Act & Assert
                await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync(url));
            }
        }

        [Theory]
        [InlineData(Tls10Name, Tls10TestUrl)]
        [InlineData(Tls11Name, Tls11TestUrl)]
        [InlineData(Tls12Name, Tls12TestUrl)]
        public async Task NetworkProtocolUtility_SupportedProtocols(string name, string url)
        {
            // Arrange
            NetworkProtocolUtility.ConfigureSupportedSslProtocols();
            var client = new HttpClient();

            // Act
            using (var response = await client.GetAsync(url))
            {
                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }
        }
    }
}
