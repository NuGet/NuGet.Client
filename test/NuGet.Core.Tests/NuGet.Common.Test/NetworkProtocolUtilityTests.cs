// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
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

        [Theory]
        [InlineData(Ssl20Name, Ssl20TestUrl, false)] // not supported
        //[InlineData(Ssl30Name, Ssl30TestUrl, null)]  // platform-specific support
        [InlineData(Tls10Name, Tls10TestUrl, true)]  // supported everywhere
        [InlineData(Tls11Name, Tls11TestUrl, true)]  // supported everywhere
        [InlineData(Tls12Name, Tls12TestUrl, true)]  // supported everywhere
        public async Task NetworkProtocolUtility_SupportedProtocols(string name, string url, bool? supported)
        {
            // Arrange
            NetworkProtocolUtility.ConfigureSupportedSslProtocols();
            var client = new HttpClient();

            if (!supported.HasValue)
            {
#if IS_CORECLR
                // .NET Core bug: https://github.com/dotnet/corefx/issues/6668
                if (name == Ssl30Name)
                {
                    supported = RuntimeEnvironmentHelper.IsLinux;
                }
#else
                if (name == Ssl30Name)
                {
                    supported = false;
                }
#endif
            }

            if (supported.Value)
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
    }
}
