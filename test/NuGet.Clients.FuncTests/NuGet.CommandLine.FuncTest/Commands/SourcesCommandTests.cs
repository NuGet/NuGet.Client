using System;
using System.IO;
using System.Linq;
using System.Net;
using NuGet.CommandLine.Test;
using NuGet.CommandLine.Test.Caching;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.CommandLine.FuncTest.Commands
{
    public class SourcesCommandTests
    {
        [Fact]
        public void Add_WithTrustAndSupportedPackageSource_Success()
        {
            using (var preserver = new DefaultConfigurationFilePreserver())
            using (var mockServer = new MockServer())
            {
                // Arrange
                var baseUrl = mockServer.Uri.TrimEnd(new[] { '/' });
                var builder = new MockResponseBuilder(baseUrl);

                mockServer.Get.Add(
                    builder.GetV3IndexPath(),
                    request =>
                    {
                        return new Action<HttpListenerResponse>(response =>
                        {
                            var mockResponse = builder.BuildV3IndexWithRepoSignResponse(mockServer);
                            response.ContentType = mockResponse.ContentType;
                            MockServer.SetResponseContent(response, mockResponse.Content);
                        });
                    });

                mockServer.Get.Add(
                    builder.GetRepoSignIndexPath(),
                    request =>
                    {
                        return new Action<HttpListenerResponse>(response =>
                        {
                            var mockResponse = builder.BuildRepoSignIndexResponse();
                            response.ContentType = mockResponse.ContentType;
                            MockServer.SetResponseContent(response, mockResponse.Content);
                        });
                    });

                var nugetexe = Util.GetNuGetExePath();
                var args = new string[] {
                    "sources",
                    "Add",
                    "-Name",
                    "test_source",
                    "-Source",
                    builder.GetV3Source(),
                    "-Trust"
                };
                var root = Directory.GetDirectoryRoot(Directory.GetCurrentDirectory());

                mockServer.Start();

                // Act
                var result = CommandRunner.Run(nugetexe, root, string.Join(" ", args), true);

                // Assert'
                mockServer.Stop();
                Assert.Equal(0, result.Item1);
                var settings = Settings.LoadDefaultSettings(null, null, null);
                var source = new PackageSourceProvider(settings).LoadPackageSources().Where(p => p.Name == "test_source").FirstOrDefault();

                Assert.Equal(builder.GetV3Source(), source.Source);
                Assert.NotNull(source.TrustedSource);
                Assert.Null(source.TrustedSource.ServiceIndex);
                Assert.Equal(1, source.TrustedSource.Certificates.Count);

                var cert = source.TrustedSource.Certificates.FirstOrDefault();

                Assert.Equal("3f9001ea83c560d712c24cf213c3d312cb3bff51ee89435d3430bd06b5d0eece", cert.Fingerprint);
                Assert.Equal(HashAlgorithmName.SHA256, cert.FingerprintAlgorithm);
                Assert.Equal("CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US", cert.SubjectName);
            }
        }

        [Fact]
        public void Update_WithTrustAndSupportedPackageSource_Success()
        {
            // Arrange
            var nugetexe = Util.GetNuGetExePath();

            using (var configFileDirectory = TestDirectory.Create())
            using (var mockServer = new MockServer())
            {
                var configFileName = "nuget.config";
                var configFilePath = Path.Combine(configFileDirectory, configFileName);

                // Arrange
                var baseUrl = mockServer.Uri.TrimEnd(new[] { '/' });
                var builder = new MockResponseBuilder(baseUrl);

                mockServer.Get.Add(
                    builder.GetV3IndexPath(),
                    request =>
                    {
                        return new Action<HttpListenerResponse>(response =>
                        {
                            var mockResponse = builder.BuildV3IndexWithRepoSignResponse(mockServer);
                            response.ContentType = mockResponse.ContentType;
                            MockServer.SetResponseContent(response, mockResponse.Content);
                        });
                    });

                mockServer.Get.Add(
                    builder.GetRepoSignIndexPath(),
                    request =>
                    {
                        return new Action<HttpListenerResponse>(response =>
                        {
                            var mockResponse = builder.BuildRepoSignIndexResponse();
                            response.ContentType = mockResponse.ContentType;
                            MockServer.SetResponseContent(response, mockResponse.Content);
                        });
                    });

                Util.CreateFile(configFileDirectory, configFileName,
                    $@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""test_source"" value=""{builder.GetV3Source()}"" />
  </packageSources>
  <trustedSources>
    <test_source>
      <add key=""teskey"" value=""testvalue"" fingerprintAlgorithm=""SHA123"" />
    </test_source>
  </trustedSources>
</configuration>");

                var args = new string[] {
                    "sources",
                    "update",
                    "-Name",
                    "test_source",
                    "-ConfigFile",
                    configFilePath,
                    "-Trust"
                };

                var root = Directory.GetDirectoryRoot(Directory.GetCurrentDirectory());

                mockServer.Start();

                // Act
                var result = CommandRunner.Run(nugetexe, root, string.Join(" ", args), true);

                // Assert
                mockServer.Stop();

                Assert.Equal(0, result.Item1);
                var settings = Settings.LoadDefaultSettings(configFileDirectory, configFileName, null);
                var source = new PackageSourceProvider(settings).LoadPackageSources().Where(p => p.Name == "test_source").FirstOrDefault();

                Assert.Equal(builder.GetV3Source(), source.Source);
                Assert.NotNull(source.TrustedSource);
                Assert.Null(source.TrustedSource.ServiceIndex);
                Assert.Equal(1, source.TrustedSource.Certificates.Count);

                var cert = source.TrustedSource.Certificates.FirstOrDefault();

                Assert.Equal("3f9001ea83c560d712c24cf213c3d312cb3bff51ee89435d3430bd06b5d0eece", cert.Fingerprint);
                Assert.Equal(HashAlgorithmName.SHA256, cert.FingerprintAlgorithm);
                Assert.Equal("CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US", cert.SubjectName);
            }
        }

        [Fact]
        public void Update_WithoutTrustAndSupportedPackageSource_Success()
        {
            // Arrange
            var nugetexe = Util.GetNuGetExePath();

            using (var configFileDirectory = TestDirectory.Create())
            using (var mockServer = new MockServer())
            {
                var configFileName = "nuget.config";
                var configFilePath = Path.Combine(configFileDirectory, configFileName);

                // Arrange
                var baseUrl = mockServer.Uri.TrimEnd(new[] { '/' });
                var builder = new MockResponseBuilder(baseUrl);

                mockServer.Get.Add(
                    builder.GetV3IndexPath(),
                    request =>
                    {
                        return new Action<HttpListenerResponse>(response =>
                        {
                            var mockResponse = builder.BuildV3IndexWithRepoSignResponse(mockServer);
                            response.ContentType = mockResponse.ContentType;
                            MockServer.SetResponseContent(response, mockResponse.Content);
                        });
                    });

                mockServer.Get.Add(
                    builder.GetRepoSignIndexPath(),
                    request =>
                    {
                        return new Action<HttpListenerResponse>(response =>
                        {
                            var mockResponse = builder.BuildRepoSignIndexResponse();
                            response.ContentType = mockResponse.ContentType;
                            MockServer.SetResponseContent(response, mockResponse.Content);
                        });
                    });

                Util.CreateFile(configFileDirectory, configFileName,
                    $@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""test_source"" value=""{builder.GetV3Source()}"" />
  </packageSources>
  <trustedSources>
    <test_source>
      <add key=""3f9001ea83c560d712c24cf213c3d312cb3bff51ee89435d3430bd06b5d0eece"" value=""CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US"" fingerprintAlgorithm=""SHA256"" />
    </test_source>
  </trustedSources>
</configuration>");

                var args = new string[] {
                    "sources",
                    "update",
                    "-Name",
                    "test_source",
                    "-Source",
                    "http://test_source",
                    "-ConfigFile",
                    configFilePath
                };

                var root = Directory.GetDirectoryRoot(Directory.GetCurrentDirectory());

                mockServer.Start();

                // Act
                var result = CommandRunner.Run(nugetexe, root, string.Join(" ", args), true);

                // Assert
                mockServer.Stop();

                Assert.Equal(0, result.Item1);
                var settings = Settings.LoadDefaultSettings(configFileDirectory, configFileName, null);
                var source = new PackageSourceProvider(settings).LoadPackageSources().Where(p => p.Name == "test_source").FirstOrDefault();

                Assert.Equal("http://test_source", source.Source);
                Assert.NotNull(source.TrustedSource);
                Assert.Null(source.TrustedSource.ServiceIndex);
                Assert.Equal(1, source.TrustedSource.Certificates.Count);

                var cert = source.TrustedSource.Certificates.FirstOrDefault();

                Assert.Equal("3f9001ea83c560d712c24cf213c3d312cb3bff51ee89435d3430bd06b5d0eece", cert.Fingerprint);
                Assert.Equal(HashAlgorithmName.SHA256, cert.FingerprintAlgorithm);
                Assert.Equal("CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US", cert.SubjectName);
            }
        }

        [Fact]
        public void Add_WithTrustAndUnsupportPackageSource_Fail()
        {
            using (var preserver = new DefaultConfigurationFilePreserver())
            using (var mockServer = new MockServer())
            {
                // Arrange
                var baseUrl = mockServer.Uri.TrimEnd(new[] { '/' });
                var builder = new MockResponseBuilder(baseUrl);

                mockServer.Get.Add(
                    builder.GetV3IndexPath(),
                    request =>
                    {
                        return new Action<HttpListenerResponse>(response =>
                        {
                            var mockResponse = builder.BuildV3IndexResponse(mockServer);
                            response.ContentType = mockResponse.ContentType;
                            MockServer.SetResponseContent(response, mockResponse.Content);
                        });
                    });

                var nugetexe = Util.GetNuGetExePath();
                var args = new string[] {
                    "sources",
                    "Add",
                    "-Name",
                    "test_source",
                    "-Source",
                    builder.GetV3Source(),
                    "-Trust"
                };
                var root = Directory.GetDirectoryRoot(Directory.GetCurrentDirectory());

                mockServer.Start();

                // Act
                var result = CommandRunner.Run(nugetexe, root, string.Join(" ", args), true);

                // Assert'
                mockServer.Stop();
                Assert.Equal(1, result.Item1);
                Assert.True(result.Item3.Contains("Package source with name 'test_source' cannot be added as a trusted repository because it does not support repository signing."));
            }
        }

        [Fact]
        public void Update_WithTrustAndUnsupportPackageSource_Fail()
        {
            using (var configFileDirectory = TestDirectory.Create())
            {
                // Arrange
                var configFileName = "nuget.config";
                var configFilePath = Path.Combine(configFileDirectory, configFileName);

                Util.CreateFile(configFileDirectory, configFileName,
                    $@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""test_source"" value=""http://test_source"" />
  </packageSources>
  <trustedSources>
    <test_source>
      <add key=""3f9001ea83c560d712c24cf213c3d312cb3bff51ee89435d3430bd06b5d0eece"" value=""CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US"" fingerprintAlgorithm=""SHA256"" />
    </test_source>
  </trustedSources>
</configuration>");

                var nugetexe = Util.GetNuGetExePath();
                var args = new string[] {
                    "sources",
                    "update",
                    "-Name",
                    "test_source",
                    "-ConfigFile",
                    configFilePath,
                    "-Trust"
                };
                var root = Directory.GetDirectoryRoot(Directory.GetCurrentDirectory());

                // Act
                var result = CommandRunner.Run(nugetexe, root, string.Join(" ", args), true);

                // Assert
                Assert.Equal(1, result.Item1);
                Assert.True(result.Item3.Contains("Package source with name 'test_source' cannot be added as a trusted repository because it does not support repository signing."));
            }
        }
    }
}
