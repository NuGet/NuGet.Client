using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Test.Utility;
using Xunit;

namespace NuGet.Protocol.Tests
{
    public class V2FeedParserTests
    {
        [Fact]
        public async Task V2FeedParser_Basic()
        {
            // Arrange
            var serviceAddress = TestUtility.CreateServiceAddress();

            var responses = new Dictionary<string, string>();
            responses.Add(serviceAddress + "FindPackagesById()?id='WindowsAzure.Storage'",
                TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.WindowsAzureStorageFindPackagesById.xml", GetType()));
            responses.Add(serviceAddress, string.Empty);

            var httpSource = new TestHttpSource(new PackageSource(serviceAddress), responses);

            V2FeedParser parser = new V2FeedParser(httpSource, serviceAddress);

            // Act
            var packages = await parser.FindPackagesByIdAsync(
                "WindowsAzure.Storage",
                NullLogger.Instance,
                CancellationToken.None);

            // Assert
            Assert.Equal(47, packages.Count());
        }

        [Fact]
        public async Task V2FeedParser_FollowNextLinks()
        {
            // Arrange
            var serviceAddress = TestUtility.CreateServiceAddress();

            var responses = new Dictionary<string, string>();
            responses.Add(serviceAddress + "FindPackagesById()?id='ravendb.client'",
                TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.RavendbFindPackagesById.xml", GetType()));
            responses.Add(serviceAddress, string.Empty);
            responses.Add("https://www.nuget.org/api/v2/FindPackagesById?id='ravendb.client'&$skiptoken='RavenDB.Client','1.2.2067-Unstable'",
               TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.RavendbFindPackagesByIdPage1.xml", GetType()));
            responses.Add("https://www.nuget.org/api/v2/FindPackagesById?id='ravendb.client'&$skiptoken='RavenDB.Client','2.0.2183-Unstable'",
                TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.RavendbFindPackagesByIdPage2.xml", GetType()));

            var httpSource = new TestHttpSource(new PackageSource(serviceAddress), responses);

            V2FeedParser parser = new V2FeedParser(httpSource, serviceAddress);

            // Act
            var packages = await parser.FindPackagesByIdAsync("ravendb.client", NullLogger.Instance, CancellationToken.None);

            // Assert
            Assert.Equal(300, packages.Count());
        }

        [Fact]
        public async Task V2FeedParser_FindPackagesByIdAsync()
        {
            // Arrange
            var serviceAddress = TestUtility.CreateServiceAddress();

            var responses = new Dictionary<string, string>();
            responses.Add(serviceAddress + "FindPackagesById()?id='WindowsAzure.Storage'",
                TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.WindowsAzureStorageFindPackagesById.xml", GetType()));
            responses.Add(serviceAddress, string.Empty);

            var httpSource = new TestHttpSource(new PackageSource(serviceAddress), responses);

            V2FeedParser parser = new V2FeedParser(httpSource, serviceAddress);

            // Act
            var packages = await parser.FindPackagesByIdAsync("WindowsAzure.Storage", NullLogger.Instance, CancellationToken.None);

            var latest = packages.OrderByDescending(e => e.Version, VersionComparer.VersionRelease).FirstOrDefault();

            // Assert
            Assert.Equal("WindowsAzure.Storage", latest.Id);
            Assert.Equal("6.2.2-preview", latest.Version.ToNormalizedString());
            Assert.Equal("WindowsAzure.Storage", latest.Title);
            Assert.Equal("Microsoft", String.Join(",", latest.Authors));
            Assert.Equal("", String.Join(",", latest.Owners));
            Assert.True(latest.Description.StartsWith("This client library enables"));
            Assert.Equal(3957668, latest.DownloadCountAsInt);
            Assert.Equal("https://www.nuget.org/api/v2/package/WindowsAzure.Storage/6.2.2-preview", latest.DownloadUrl);
            Assert.Equal("http://go.microsoft.com/fwlink/?LinkID=288890", latest.IconUrl);
            Assert.Equal("http://go.microsoft.com/fwlink/?LinkId=331471", latest.LicenseUrl);
            Assert.Equal("http://go.microsoft.com/fwlink/?LinkId=235168", latest.ProjectUrl);
            Assert.Equal(DateTimeOffset.Parse("2015-12-11T01:25:11.37"), latest.Published.Value);
            Assert.Equal("https://www.nuget.org/package/ReportAbuse/WindowsAzure.Storage/6.2.2-preview", latest.ReportAbuseUrl);
            Assert.True(latest.RequireLicenseAcceptance);
            Assert.Equal("A client library for working with Microsoft Azure storage services including blobs, files, tables, and queues.", latest.Summary);
            Assert.Equal("Microsoft Azure Storage Table Blob File Queue Scalable windowsazureofficial", latest.Tags);
            Assert.Equal("Microsoft.Data.OData:5.6.4:dotnet54|Microsoft.Data.Services.Client:5.6.4:dotnet54|System.Spatial:5.6.4:dotnet54|Newtonsoft.Json:6.0.8:dotnet54|System.Collections:4.0.11-beta-23225:dotnet54|System.Collections.Concurrent:4.0.11-beta-23225:dotnet54|System.Collections.Specialized:4.0.0-beta-23109:dotnet54|System.Diagnostics.Debug:4.0.11-beta-23225:dotnet54|System.Diagnostics.Tools:4.0.1-beta-23225:dotnet54|System.Diagnostics.TraceSource:4.0.0-beta-23225:dotnet54|System.Diagnostics.Tracing:4.0.21-beta-23225:dotnet54|System.Dynamic.Runtime:4.0.11-beta-23225:dotnet54|System.Globalization:4.0.11-beta-23225:dotnet54|System.IO:4.0.11-beta-23225:dotnet54|System.IO.FileSystem:4.0.1-beta-23225:dotnet54|System.IO.FileSystem.Primitives:4.0.1-beta-23225:dotnet54|System.Linq:4.0.1-beta-23225:dotnet54|System.Linq.Expressions:4.0.11-beta-23225:dotnet54|System.Linq.Queryable:4.0.0-beta-23109:dotnet54|System.Net.Http:4.0.1-beta-23225:dotnet54|System.Net.Primitives:4.0.11-beta-23225:dotnet54|System.Reflection:4.1.0-beta-23225:dotnet54|System.Reflection.Extensions:4.0.1-beta-23225:dotnet54|System.Reflection.TypeExtensions:4.0.1-beta-23225:dotnet54|System.Runtime:4.0.20-beta-23109:dotnet54|System.Runtime.Extensions:4.0.11-beta-23225:dotnet54|System.Runtime.InteropServices:4.0.21-beta-23225:dotnet54|System.Runtime.Serialization.Primitives:4.0.0-beta-23109:dotnet54|System.Runtime.Serialization.Xml:4.0.10-beta-23109:dotnet54|System.Security.Cryptography.Encoding:4.0.0-beta-23225:dotnet54|System.Security.Cryptography.Primitives:4.0.0-beta-23225:dotnet54|System.Security.Cryptography.Algorithms:4.0.0-beta-23225:dotnet54|System.Text.Encoding:4.0.11-beta-23225:dotnet54|System.Text.Encoding.Extensions:4.0.11-beta-23225:dotnet54|System.Text.RegularExpressions:4.0.11-beta-23225:dotnet54|System.Threading:4.0.11-beta-23225:dotnet54|System.Threading.Tasks:4.0.11-beta-23225:dotnet54|System.Threading.Thread:4.0.0-beta-23225:dotnet54|System.Threading.ThreadPool:4.0.10-beta-23225:dotnet54|System.Threading.Timer:4.0.1-beta-23225:dotnet54|System.Xml.ReaderWriter:4.0.11-beta-23225:dotnet54|System.Xml.XDocument:4.0.11-beta-23225:dotnet54|System.Xml.XmlSerializer:4.0.0-beta-23109:dotnet54|Microsoft.Data.OData:5.6.4:net40-Client|Newtonsoft.Json:6.0.8:net40-Client|Microsoft.Data.Services.Client:5.6.4:net40-Client|Microsoft.Azure.KeyVault.Core:1.0.0:net40-Client|Microsoft.Data.OData:5.6.4:win80|Newtonsoft.Json:6.0.8:win80|Microsoft.Data.OData:5.6.4:wpa|Newtonsoft.Json:6.0.8:wpa|Microsoft.Data.OData:5.6.4:wp80|Newtonsoft.Json:6.0.8:wp80|Microsoft.Azure.KeyVault.Core:1.0.0:wp80|Microsoft.Data.OData:5.6.4:portable-net45+win+wpa81+MonoAndroid10+MonoTouch10|Newtonsoft.Json:6.0.8:portable-net45+win+wpa81+MonoAndroid10+MonoTouch10", latest.Dependencies);
            Assert.Equal(6, latest.DependencySets.Count());
            Assert.Equal("dotnet5.4", latest.DependencySets.First().TargetFramework.GetShortFolderName());
        }

        [Fact]
        public async Task V2FeedParser_DownloadFromUrl()
        {
            // Arrange
            var repo = Repository.Factory.GetCoreV3("https://www.nuget.org/api/v2/");

            var httpSource = HttpSource.Create(repo);

            V2FeedParser parser = new V2FeedParser(httpSource, "https://www.nuget.org/api/v2/");

            // Act & Assert
            using (var downloadResult = await parser.DownloadFromUrl(new PackageIdentity("WindowsAzure.Storage", new NuGetVersion("6.2.0")),
                                                              new Uri("https://www.nuget.org/api/v2/package/WindowsAzure.Storage/6.2.0"),
                                                              Configuration.NullSettings.Instance,
                                                              new SourceCacheContext(),
                                                              NullLogger.Instance,
                                                              CancellationToken.None))
            {
                var packageReader = downloadResult.PackageReader;
                var files = packageReader.GetFiles();

                Assert.Equal(11, files.Count());
            }
        }



        [Fact]
        public async Task V2FeedParser_DownloadFromIdentityInvalidId()
        {
            // Arrange
            var serviceAddress = TestUtility.CreateServiceAddress();

            var responses = new Dictionary<string, string>();
            responses.Add(serviceAddress + "Packages(Id='xunit',Version='1.0.0-notfound')", string.Empty);
            responses.Add(serviceAddress + "FindPackagesById()?id='xunit'",
                TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.XunitFindPackagesById.xml", GetType()));
            responses.Add(serviceAddress, string.Empty);

            var httpSource = new TestHttpSource(new PackageSource(serviceAddress), responses,
                TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.500Error.xml", GetType()));
            V2FeedParser parser = new V2FeedParser(httpSource, serviceAddress);

            // Act
            var actual = await parser.DownloadFromIdentity(new PackageIdentity("xunit", new NuGetVersion("1.0.0-notfound")),
                NullSettings.Instance,
                new SourceCacheContext(),
                NullLogger.Instance,
                CancellationToken.None);

            // Assert
            Assert.NotNull(actual);
            Assert.Equal(DownloadResourceResultStatus.NotFound, actual.Status);
        }

        [Fact]
        public async Task V2FeedParser_Search()
        {
            // Arrange
            var serviceAddress = TestUtility.CreateServiceAddress();

            var responses = new Dictionary<string, string>();
            responses.Add(serviceAddress + "Search()?$filter=IsLatestVersion&searchTerm='azure'&targetFramework='net40-client'&includePrerelease=false&$skip=0&$top=1",
                TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.AzureSearch.xml", GetType()));
            responses.Add(serviceAddress, string.Empty);

            var httpSource = new TestHttpSource(new PackageSource(serviceAddress), responses);

            V2FeedParser parser = new V2FeedParser(httpSource, serviceAddress);
            var searchFilter = new SearchFilter()
            {
                IncludePrerelease = false,
                SupportedFrameworks = new string[] { "net40-Client" }
            };

            // Act
            var packages = await parser.Search("azure", searchFilter, 0, 1, NullLogger.Instance, CancellationToken.None);
            var package = packages.FirstOrDefault();

            // Assert
            Assert.Equal("WindowsAzure.Storage", package.Id);
            Assert.Equal("6.2.0", package.Version.ToNormalizedString());
            Assert.Equal("WindowsAzure.Storage", package.Title);
            Assert.Equal("Microsoft", String.Join(",", package.Authors));
            Assert.Equal("", String.Join(",", package.Owners));
            Assert.True(package.Description.StartsWith("This client library enables"));
            Assert.Equal(3957668, package.DownloadCountAsInt);
            Assert.Equal("https://www.nuget.org/api/v2/package/WindowsAzure.Storage/6.2.0", package.DownloadUrl);
            Assert.Equal("http://go.microsoft.com/fwlink/?LinkID=288890", package.IconUrl);
            Assert.Equal("http://go.microsoft.com/fwlink/?LinkId=331471", package.LicenseUrl);
            Assert.Equal("http://go.microsoft.com/fwlink/?LinkId=235168", package.ProjectUrl);
            Assert.Equal(DateTimeOffset.Parse("2015-12-10T22:39:05.103"), package.Published.Value);
            Assert.Equal("https://www.nuget.org/package/ReportAbuse/WindowsAzure.Storage/6.2.0", package.ReportAbuseUrl);
            Assert.True(package.RequireLicenseAcceptance);
            Assert.Equal("A client library for working with Microsoft Azure storage services including blobs, files, tables, and queues.", package.Summary);
            Assert.Equal("Microsoft Azure Storage Table Blob File Queue Scalable windowsazureofficial", package.Tags);
            Assert.Equal("Microsoft.Data.OData:5.6.4:net40-Client|Newtonsoft.Json:6.0.8:net40-Client|Microsoft.Data.Services.Client:5.6.4:net40-Client|Microsoft.Azure.KeyVault.Core:1.0.0:net40-Client|Microsoft.Data.OData:5.6.4:win80|Newtonsoft.Json:6.0.8:win80|Microsoft.Data.OData:5.6.4:wpa|Newtonsoft.Json:6.0.8:wpa|Microsoft.Data.OData:5.6.4:wp80|Newtonsoft.Json:6.0.8:wp80|Microsoft.Azure.KeyVault.Core:1.0.0:wp80", package.Dependencies);
            Assert.Equal(4, package.DependencySets.Count());
            Assert.Equal("net40-client", package.DependencySets.First().TargetFramework.GetShortFolderName());
        }

        [Fact]
        public async Task V2FeedParser_SearchEncoding()
        {
            // Arrange
            var serviceAddress = TestUtility.CreateServiceAddress();

            var responses = new Dictionary<string, string>();
            responses.Add(serviceAddress + "Search()?$filter=IsLatestVersion&searchTerm='azure%20%2B''%20b%20'&targetFramework='portable-net45%2Bwin8'&includePrerelease=false&$skip=0&$top=1",
                TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.AzureSearch.xml", GetType()));
            responses.Add(serviceAddress, string.Empty);

            var httpSource = new TestHttpSource(new PackageSource(serviceAddress), responses);

            V2FeedParser parser = new V2FeedParser(httpSource, serviceAddress);
            var searchFilter = new SearchFilter()
            {
                IncludePrerelease = false,
                SupportedFrameworks = new string[] { "portable-net45+win8" }
            };

            // Act
            var packages = await parser.Search("azure +' b ", searchFilter, 0, 1, NullLogger.Instance, CancellationToken.None);
            var package = packages.FirstOrDefault();

            // Assert
            Assert.Equal("WindowsAzure.Storage", package.Id);
        }

        [Fact]
        public async Task V2FeedParser_SearchTop100()
        {
            // Arrange
            var serviceAddress = TestUtility.CreateServiceAddress();

            var responses = new Dictionary<string, string>();
            responses.Add(serviceAddress + "Search()?$filter=IsLatestVersion&searchTerm='azure'&targetFramework='net40-client'&includePrerelease=false&$skip=0&$top=100",
                TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.AzureSearch100.xml", GetType()));
            responses.Add("https://www.nuget.org/api/v2/Search?searchTerm='azure'&targetFramework='net40-client'&includePrerelease=false&$filter=IsLatestVersion&$skiptoken='Haven.ServiceBus.Azure.ServiceBus.Publisher','1.0.5835.19676',100",
                TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.AzureSearchNext100.xml", GetType()));
            responses.Add(serviceAddress, string.Empty);

            var httpSource = new TestHttpSource(new PackageSource(serviceAddress), responses);
            V2FeedParser parser = new V2FeedParser(httpSource, serviceAddress);
            var searchFilter = new SearchFilter()
            {
                IncludePrerelease = false,
                SupportedFrameworks = new string[] { "net40-Client" }
            };

            // Act
            var packages = await parser.Search("azure", searchFilter, 0, 100, NullLogger.Instance, CancellationToken.None);

            // Assert
            Assert.Equal(100, packages.Count());
        }

        [Fact]
        public async Task V2FeedParser_Search_NotFound()
        {
            // Arrange
            var serviceAddress = TestUtility.CreateServiceAddress();

            var responses = new Dictionary<string, string>();
            responses.Add(serviceAddress + "Search()?$filter=IsLatestVersion&searchTerm='azure'&targetFramework='net40-client'&includePrerelease=false&$skip=0&$top=1",
                string.Empty);
            responses.Add(serviceAddress, string.Empty);

            var httpSource = new TestHttpSource(new PackageSource(serviceAddress), responses);

            V2FeedParser parser = new V2FeedParser(httpSource, serviceAddress);
            var searchFilter = new SearchFilter()
            {
                IncludePrerelease = false,
                SupportedFrameworks = new string[] { "net40-Client" }
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<FatalProtocolException>(() => parser.Search(
                "azure",
                searchFilter,
                0,
                1,
                NullLogger.Instance,
                CancellationToken.None));

            Assert.Equal(
                "The V2 feed at '" + serviceAddress + "Search()?$filter=IsLatestVersion&searchTerm='azure'&targetFramework='net40-client'&includePrerelease=false&$skip=0&$top=1' " +
                "returned an unexpected status code '404 Not Found'.",
                exception.Message);
        }

        [Fact]
        public async Task V2FeedParser_Search_InternalServerError()
        {
            // Arrange
            var serviceAddress = TestUtility.CreateServiceAddress();

            var responses = new Dictionary<string, string>();
            responses.Add(serviceAddress + "Search()?$filter=IsLatestVersion&searchTerm='azure'&targetFramework='net40-client'&includePrerelease=false&$skip=0&$top=1",
                null);
            responses.Add(serviceAddress, string.Empty);

            var httpSource = new TestHttpSource(new PackageSource(serviceAddress), responses);

            V2FeedParser parser = new V2FeedParser(httpSource, serviceAddress);
            var searchFilter = new SearchFilter()
            {
                IncludePrerelease = false,
                SupportedFrameworks = new string[] { "net40-Client" }
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<FatalProtocolException>(() => parser.Search(
                "azure",
                searchFilter,
                0,
                1,
                NullLogger.Instance,
                CancellationToken.None));

            Assert.Equal(
                "The V2 feed at '" + serviceAddress + "Search()?$filter=IsLatestVersion&searchTerm='azure'&targetFramework='net40-client'&includePrerelease=false&$skip=0&$top=1' " +
                "returned an unexpected status code '500 Internal Server Error'.",
                exception.Message);
        }

        [Fact]
        public async Task V2FeedParser_GetPackage()
        {
            // Arrange
            var serviceAddress = TestUtility.CreateServiceAddress();

            var responses = new Dictionary<string, string>();
            responses.Add(serviceAddress + "Packages(Id='WindowsAzure.Storage',Version='4.3.2-preview')",
                TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.WindowsAzureStorageGetPackages.xml", GetType()));
            responses.Add(serviceAddress, string.Empty);

            var httpSource = new TestHttpSource(new PackageSource(serviceAddress), responses);

            V2FeedParser parser = new V2FeedParser(httpSource, serviceAddress);

            // Act
            var package = await parser.GetPackage(new PackageIdentity("WindowsAzure.Storage", new NuGetVersion("4.3.2-preview")), NullLogger.Instance, CancellationToken.None);

            // Assert
            Assert.Equal("WindowsAzure.Storage", package.Id);
            Assert.Equal("4.3.2-preview", package.Version.ToNormalizedString());
            Assert.Equal("WindowsAzure.Storage", package.Title);
            Assert.Equal("Microsoft", String.Join(",", package.Authors));
            Assert.Equal("", String.Join(",", package.Owners));
            Assert.True(package.Description.StartsWith("This client library enables"));
            Assert.Equal(3958716, package.DownloadCountAsInt);
            Assert.Equal("https://www.nuget.org/api/v2/package/WindowsAzure.Storage/4.3.2-preview", package.DownloadUrl);
            Assert.Equal("http://go.microsoft.com/fwlink/?LinkID=288890", package.IconUrl);
            Assert.Equal("http://go.microsoft.com/fwlink/?LinkId=331471", package.LicenseUrl);
            Assert.Equal("http://go.microsoft.com/fwlink/?LinkId=235168", package.ProjectUrl);
            Assert.Equal(DateTimeOffset.Parse("2014-11-12T22:19:16.297"), package.Published.Value);
            Assert.Equal("https://www.nuget.org/package/ReportAbuse/WindowsAzure.Storage/4.3.2-preview", package.ReportAbuseUrl);
            Assert.True(package.RequireLicenseAcceptance);
            Assert.Equal("A client library for working with Microsoft Azure storage services including blobs, files, tables, and queues.", package.Summary);
            Assert.Equal("Microsoft Azure Storage Table Blob File Queue Scalable windowsazureofficial", package.Tags);
            Assert.Equal("Microsoft.Data.OData:5.6.3:aspnetcore50|Microsoft.Data.Services.Client:5.6.3:aspnetcore50|System.Spatial:5.6.3:aspnetcore50|System.Collections:4.0.10-beta-22231:aspnetcore50|System.Collections.Concurrent:4.0.0-beta-22231:aspnetcore50|System.Collections.Specialized:4.0.0-beta-22231:aspnetcore50|System.Diagnostics.Debug:4.0.10-beta-22231:aspnetcore50|System.Diagnostics.Tools:4.0.0-beta-22231:aspnetcore50|System.Diagnostics.TraceSource:4.0.0-beta-22231:aspnetcore50|System.Diagnostics.Tracing:4.0.10-beta-22231:aspnetcore50|System.Dynamic.Runtime:4.0.0-beta-22231:aspnetcore50|System.Globalization:4.0.10-beta-22231:aspnetcore50|System.IO:4.0.10-beta-22231:aspnetcore50|System.IO.FileSystem:4.0.0-beta-22231:aspnetcore50|System.IO.FileSystem.Primitives:4.0.0-beta-22231:aspnetcore50|System.Linq:4.0.0-beta-22231:aspnetcore50|System.Linq.Expressions:4.0.0-beta-22231:aspnetcore50|System.Linq.Queryable:4.0.0-beta-22231:aspnetcore50|System.Net.Http:4.0.0-beta-22231:aspnetcore50|System.Net.Primitives:4.0.10-beta-22231:aspnetcore50|System.Reflection:4.0.10-beta-22231:aspnetcore50|System.Reflection.Extensions:4.0.0-beta-22231:aspnetcore50|System.Reflection.TypeExtensions:4.0.0-beta-22231:aspnetcore50|System.Runtime:4.0.20-beta-22231:aspnetcore50|System.Runtime.Extensions:4.0.10-beta-22231:aspnetcore50|System.Runtime.InteropServices:4.0.20-beta-22231:aspnetcore50|System.Runtime.Serialization.Primitives:4.0.0-beta-22231:aspnetcore50|System.Runtime.Serialization.Xml:4.0.10-beta-22231:aspnetcore50|System.Security.Cryptography.Encoding:4.0.0-beta-22231:aspnetcore50|System.Security.Cryptography.Encryption:4.0.0-beta-22231:aspnetcore50|System.Security.Cryptography.Hashing:4.0.0-beta-22231:aspnetcore50|System.Security.Cryptography.Hashing.Algorithms:4.0.0-beta-22231:aspnetcore50|System.Text.Encoding:4.0.10-beta-22231:aspnetcore50|System.Text.Encoding.Extensions:4.0.10-beta-22231:aspnetcore50|System.Text.RegularExpressions:4.0.10-beta-22231:aspnetcore50|System.Threading:4.0.0-beta-22231:aspnetcore50|System.Threading.Tasks:4.0.10-beta-22231:aspnetcore50|System.Threading.Thread:4.0.0-beta-22231:aspnetcore50|System.Threading.ThreadPool:4.0.10-beta-22231:aspnetcore50|System.Threading.Timer:4.0.0-beta-22231:aspnetcore50|System.Xml.ReaderWriter:4.0.10-beta-22231:aspnetcore50|System.Xml.XDocument:4.0.0-beta-22231:aspnetcore50|System.Xml.XmlSerializer:4.0.0-beta-22231:aspnetcore50|Microsoft.Data.OData:5.6.3:aspnet50|Microsoft.Data.Services.Client:5.6.3:aspnet50|System.Spatial:5.6.3:aspnet50|Microsoft.Data.OData:5.6.2:net40-Client|Newtonsoft.Json:5.0.8:net40-Client|Microsoft.Data.Services.Client:5.6.2:net40-Client|Microsoft.WindowsAzure.ConfigurationManager:1.8.0.0:net40-Client|Microsoft.Data.OData:5.6.2:win80|Microsoft.Data.OData:5.6.2:wpa|Microsoft.Data.OData:5.6.2:wp80|Newtonsoft.Json:5.0.8:wp80", package.Dependencies);
            Assert.Equal(6, package.DependencySets.Count());
            Assert.Equal("aspnetcore50", package.DependencySets.First().TargetFramework.GetShortFolderName());
        }

        [Fact]
        public async Task V2FeedParser_GetPackage_NotFoundOnPackages()
        {
            // Arrange
            var serviceAddress = TestUtility.CreateServiceAddress();

            var responses = new Dictionary<string, string>();
            responses.Add(serviceAddress + "Packages(Id='xunit',Version='1.0.0-notfound')", string.Empty);
            responses.Add(serviceAddress + "FindPackagesById()?id='xunit'",
                TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.XunitFindPackagesById.xml", GetType()));
            responses.Add(serviceAddress, string.Empty);

            var httpSource = new TestHttpSource(new PackageSource(serviceAddress), responses,
                TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.500Error.xml", GetType()));

            V2FeedParser parser = new V2FeedParser(httpSource, serviceAddress);

            // Act
            var package = await parser.GetPackage(new PackageIdentity("xunit", new NuGetVersion("1.0.0-notfound")), NullLogger.Instance, CancellationToken.None);

            // Assert
            Assert.Null(package);
        }

        [Fact]
        public async Task V2FeedParser_GetPackage_NotFoundOnPackagesFoundOnFindPackagesById()
        {
            // Arrange
            var serviceAddress = TestUtility.CreateServiceAddress();

            var responses = new Dictionary<string, string>();
            responses.Add(serviceAddress + "Packages(Id='WindowsAzure.Storage',Version='4.3.2-preview')", string.Empty);
            responses.Add(serviceAddress + "FindPackagesById()?id='WindowsAzure.Storage'",
                TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.WindowsAzureStorageFindPackagesById.xml", GetType()));
            responses.Add(serviceAddress, string.Empty);

            var httpSource = new TestHttpSource(new PackageSource(serviceAddress), responses);

            V2FeedParser parser = new V2FeedParser(httpSource, serviceAddress);
            var packageIdentity = new PackageIdentity("WindowsAzure.Storage", new NuGetVersion("4.3.2-preview"));

            // Act
            var package = await parser.GetPackage(packageIdentity, NullLogger.Instance, CancellationToken.None);

            // Assert
            Assert.Equal("WindowsAzure.Storage", package.Id);
            Assert.Equal("4.3.2-preview", package.Version.ToNormalizedString());
            Assert.Equal("WindowsAzure.Storage", package.Title);
            Assert.Equal("Microsoft", String.Join(",", package.Authors));
            Assert.Equal("", String.Join(",", package.Owners));
            Assert.True(package.Description.StartsWith("This client library enables"));
            Assert.Equal(3957668, package.DownloadCountAsInt);
            Assert.Equal("https://www.nuget.org/api/v2/package/WindowsAzure.Storage/4.3.2-preview", package.DownloadUrl);
            Assert.Equal("http://go.microsoft.com/fwlink/?LinkID=288890", package.IconUrl);
            Assert.Equal("http://go.microsoft.com/fwlink/?LinkId=331471", package.LicenseUrl);
            Assert.Equal("http://go.microsoft.com/fwlink/?LinkId=235168", package.ProjectUrl);
            Assert.Equal(DateTimeOffset.Parse("2014-11-12T22:19:16.297"), package.Published.Value);
            Assert.Equal("https://www.nuget.org/package/ReportAbuse/WindowsAzure.Storage/4.3.2-preview", package.ReportAbuseUrl);
            Assert.True(package.RequireLicenseAcceptance);
            Assert.Equal("A client library for working with Microsoft Azure storage services including blobs, files, tables, and queues.", package.Summary);
            Assert.Equal("Microsoft Azure Storage Table Blob File Queue Scalable windowsazureofficial", package.Tags);
            Assert.Equal("Microsoft.Data.OData:5.6.3:aspnetcore50|Microsoft.Data.Services.Client:5.6.3:aspnetcore50|System.Spatial:5.6.3:aspnetcore50|System.Collections:4.0.10-beta-22231:aspnetcore50|System.Collections.Concurrent:4.0.0-beta-22231:aspnetcore50|System.Collections.Specialized:4.0.0-beta-22231:aspnetcore50|System.Diagnostics.Debug:4.0.10-beta-22231:aspnetcore50|System.Diagnostics.Tools:4.0.0-beta-22231:aspnetcore50|System.Diagnostics.TraceSource:4.0.0-beta-22231:aspnetcore50|System.Diagnostics.Tracing:4.0.10-beta-22231:aspnetcore50|System.Dynamic.Runtime:4.0.0-beta-22231:aspnetcore50|System.Globalization:4.0.10-beta-22231:aspnetcore50|System.IO:4.0.10-beta-22231:aspnetcore50|System.IO.FileSystem:4.0.0-beta-22231:aspnetcore50|System.IO.FileSystem.Primitives:4.0.0-beta-22231:aspnetcore50|System.Linq:4.0.0-beta-22231:aspnetcore50|System.Linq.Expressions:4.0.0-beta-22231:aspnetcore50|System.Linq.Queryable:4.0.0-beta-22231:aspnetcore50|System.Net.Http:4.0.0-beta-22231:aspnetcore50|System.Net.Primitives:4.0.10-beta-22231:aspnetcore50|System.Reflection:4.0.10-beta-22231:aspnetcore50|System.Reflection.Extensions:4.0.0-beta-22231:aspnetcore50|System.Reflection.TypeExtensions:4.0.0-beta-22231:aspnetcore50|System.Runtime:4.0.20-beta-22231:aspnetcore50|System.Runtime.Extensions:4.0.10-beta-22231:aspnetcore50|System.Runtime.InteropServices:4.0.20-beta-22231:aspnetcore50|System.Runtime.Serialization.Primitives:4.0.0-beta-22231:aspnetcore50|System.Runtime.Serialization.Xml:4.0.10-beta-22231:aspnetcore50|System.Security.Cryptography.Encoding:4.0.0-beta-22231:aspnetcore50|System.Security.Cryptography.Encryption:4.0.0-beta-22231:aspnetcore50|System.Security.Cryptography.Hashing:4.0.0-beta-22231:aspnetcore50|System.Security.Cryptography.Hashing.Algorithms:4.0.0-beta-22231:aspnetcore50|System.Text.Encoding:4.0.10-beta-22231:aspnetcore50|System.Text.Encoding.Extensions:4.0.10-beta-22231:aspnetcore50|System.Text.RegularExpressions:4.0.10-beta-22231:aspnetcore50|System.Threading:4.0.0-beta-22231:aspnetcore50|System.Threading.Tasks:4.0.10-beta-22231:aspnetcore50|System.Threading.Thread:4.0.0-beta-22231:aspnetcore50|System.Threading.ThreadPool:4.0.10-beta-22231:aspnetcore50|System.Threading.Timer:4.0.0-beta-22231:aspnetcore50|System.Xml.ReaderWriter:4.0.10-beta-22231:aspnetcore50|System.Xml.XDocument:4.0.0-beta-22231:aspnetcore50|System.Xml.XmlSerializer:4.0.0-beta-22231:aspnetcore50|Microsoft.Data.OData:5.6.3:aspnet50|Microsoft.Data.Services.Client:5.6.3:aspnet50|System.Spatial:5.6.3:aspnet50|Microsoft.Data.OData:5.6.2:net40-Client|Newtonsoft.Json:5.0.8:net40-Client|Microsoft.Data.Services.Client:5.6.2:net40-Client|Microsoft.WindowsAzure.ConfigurationManager:1.8.0.0:net40-Client|Microsoft.Data.OData:5.6.2:win80|Microsoft.Data.OData:5.6.2:wpa|Microsoft.Data.OData:5.6.2:wp80|Newtonsoft.Json:5.0.8:wp80", package.Dependencies);
            Assert.Equal(6, package.DependencySets.Count());
            Assert.Equal("aspnetcore50", package.DependencySets.First().TargetFramework.GetShortFolderName());
        }

        [Fact]
        public async Task V2FeedParser_GetPackage_NotFoundOnBoth()
        {
            // Arrange
            var serviceAddress = TestUtility.CreateServiceAddress();

            var responses = new Dictionary<string, string>();
            responses.Add(serviceAddress + "Packages(Id='xunit',Version='1.0.0-notfound')", string.Empty);
            responses.Add(serviceAddress + "FindPackagesById()?id='xunit'", string.Empty);
            responses.Add(serviceAddress, string.Empty);

            var httpSource = new TestHttpSource(new PackageSource(serviceAddress), responses,
                TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.500Error.xml", GetType()));

            V2FeedParser parser = new V2FeedParser(httpSource, serviceAddress);
            var packageIdentity = new PackageIdentity("xunit", new NuGetVersion("1.0.0-notfound"));

            // Act

            var exception = await Assert.ThrowsAsync<FatalProtocolException>(() => parser.GetPackage(
                packageIdentity,
                NullLogger.Instance,
                CancellationToken.None));

            Assert.Equal(
                "The V2 feed at '" + serviceAddress + "FindPackagesById()?id='xunit'' " +
                "returned an unexpected status code '404 Not Found'.",
                exception.Message);
        }

        [Fact]
        public async Task V2FeedParser_GetPackage_InternalServerError()
        {
            // Arrange
            var serviceAddress = TestUtility.CreateServiceAddress();

            var responses = new Dictionary<string, string>();
            responses.Add(serviceAddress + "Packages(Id='xunit',Version='1.0.0-InternalServerError')", null);
            responses.Add(serviceAddress, string.Empty);

            var httpSource = new TestHttpSource(new PackageSource(serviceAddress), responses,
                TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.500Error.xml", GetType()));

            V2FeedParser parser = new V2FeedParser(httpSource, serviceAddress);
            var packageIdentity = new PackageIdentity("xunit", new NuGetVersion("1.0.0-InternalServerError"));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<FatalProtocolException>(() => parser.GetPackage(
                packageIdentity,
                NullLogger.Instance,
                CancellationToken.None));
            Assert.Equal(
                "The V2 feed at '" + serviceAddress + "Packages(Id='xunit',Version='1.0.0-InternalServerError')' " +
                "returned an unexpected status code '500 Internal Server Error'.",
                exception.Message);
        }

        [Fact]
        public async Task V2FeedParser_FindPackagesById_NotFound()
        {
            // Arrange
            var serviceAddress = TestUtility.CreateServiceAddress();

            var responses = new Dictionary<string, string>();
            responses.Add(serviceAddress + "FindPackagesById()?id='xunit'", string.Empty);
            responses.Add(serviceAddress, string.Empty);

            var httpSource = new TestHttpSource(new PackageSource(serviceAddress), responses,
                TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.500Error.xml", GetType()));

            V2FeedParser parser = new V2FeedParser(httpSource, serviceAddress);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<FatalProtocolException>(() => parser.FindPackagesByIdAsync(
                "xunit",
                NullLogger.Instance,
                CancellationToken.None));

            Assert.Equal(
                "The V2 feed at '" + serviceAddress + "FindPackagesById()?id='xunit'' " +
                "returned an unexpected status code '404 Not Found'.",
                exception.Message);
        }

        [Fact]
        public async Task V2FeedParser_FindPackagesById_InternalServerError()
        {
            // Arrange
            var serviceAddress = TestUtility.CreateServiceAddress();

            var responses = new Dictionary<string, string>();
            responses.Add(serviceAddress + "FindPackagesById()?id='xunit'", null);
            responses.Add(serviceAddress, string.Empty);

            var httpSource = new TestHttpSource(new PackageSource(serviceAddress), responses,
                TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.500Error.xml", GetType()));

            V2FeedParser parser = new V2FeedParser(httpSource, serviceAddress);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<FatalProtocolException>(() => parser.FindPackagesByIdAsync(
                "xunit",
                NullLogger.Instance,
                CancellationToken.None));

            Assert.Equal(
                "The V2 feed at '" + serviceAddress + "FindPackagesById()?id='xunit'' " +
                "returned an unexpected status code '500 Internal Server Error'.",
                exception.Message);
        }

        [Fact]
        public async Task V2FeedParser_NexusFindPackagesByIdNullDependencyRange()
        {
            // Arrange
            var serviceAddress = TestUtility.CreateServiceAddress();

            var responses = new Dictionary<string, string>();
            responses.Add(serviceAddress + "FindPackagesById()?id='PackageA'",
                TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.NexusFindPackagesById.xml", GetType()));
            responses.Add(serviceAddress, string.Empty);

            var httpSource = new TestHttpSource(new PackageSource(serviceAddress), responses);

            V2FeedParser parser = new V2FeedParser(httpSource, serviceAddress);

            // Act
            var packages = await parser.FindPackagesByIdAsync("PackageA", NullLogger.Instance, CancellationToken.None);

            var latest = packages.OrderByDescending(e => e.Version, VersionComparer.VersionRelease).FirstOrDefault();

            // Assert
            Assert.Equal("PackageA", latest.Id);
            Assert.Equal("1.0.0.99", latest.Version.ToNormalizedString());
            Assert.Equal("My Name", String.Join(",", latest.Authors));
            Assert.Equal("", String.Join(",", latest.Owners));
            Assert.True(latest.Description.StartsWith("Some description"));
            Assert.Equal(0, latest.DownloadCountAsInt);
            Assert.Equal("PackageB:null|EntityFramework:6.1.3|PackageC:3.7.0.15", latest.Dependencies);
            Assert.Equal(1, latest.DependencySets.Count());
            Assert.Equal(VersionRange.All, latest.DependencySets.Single().Packages.Where(p => p.Id == "PackageB").Single().VersionRange);
            Assert.Equal("any", latest.DependencySets.First().TargetFramework.GetShortFolderName());
        }

        [Fact]
        public async Task V2FeedParser_DuplicateNextUrl()
        {
            // Arrange
            var dupUrl =
                "https://www.nuget.org/api/v2/FindPackagesById?id='ravendb.client'&$skiptoken='RavenDB.Client','1.2.2067-Unstable'";
            var serviceAddress = TestUtility.CreateServiceAddress();

            var responses = new Dictionary<string, string>();
            responses.Add(serviceAddress + "FindPackagesById()?id='ravendb.client'",
                TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.CyclicDependency.xml", GetType()));
            responses.Add(serviceAddress, string.Empty);
            responses.Add(dupUrl,
               TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.CyclicDependencyPage1.xml", GetType()));
            responses.Add("https://www.nuget.org/api/v2/FindPackagesById?id='ravendb.client'&$skiptoken='RavenDB.Client','2.0.2183-Unstable'",
                TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.CyclicDependencyPage2.xml", GetType()));

            var httpSource = new TestHttpSource(new PackageSource(serviceAddress), responses);

            V2FeedParser parser = new V2FeedParser(httpSource, serviceAddress);

            FatalProtocolException duplicateUrlException = null;
            try
            {
                // Act
                var packages =
                    await parser.FindPackagesByIdAsync("ravendb.client", NullLogger.Instance, CancellationToken.None);
            }
            catch (FatalProtocolException ex)
            {
                duplicateUrlException = ex;
            }

            // Assert
            Assert.NotNull(duplicateUrlException);
            Assert.Equal(string.Format(CultureInfo.CurrentCulture, Strings.Protocol_duplicateUri, dupUrl), duplicateUrlException.Message);
        }

        [Fact]
        public async Task V2FeedParser_Search_MultipleSupportedFramework()
        {
            // Arrange
            var serviceAddress = TestUtility.CreateServiceAddress();

            var responses = new Dictionary<string, string>();
            responses.Add(serviceAddress + "Search()?$filter=IsLatestVersion&searchTerm='azure%20%2B''%20b%20'&targetFramework='portable45-net45%2Bwin8%2Bwpa81%7Cwpa81%7Cmonoandroid60'&includePrerelease=false&$skip=0&$top=1",
                TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.AzureSearch.xml", GetType()));
            responses.Add(serviceAddress, string.Empty);

            var httpSource = new TestHttpSource(new PackageSource(serviceAddress), responses);

            V2FeedParser parser = new V2FeedParser(httpSource, serviceAddress);
            var searchFilter = new SearchFilter()
            {
                IncludePrerelease = false,
                SupportedFrameworks = new string[]
                {
                    ".NetPortable,Version=v4.5,Profile=Profile111",
                    "WindowsPhoneApp,Version=v8.1",
                    "MonoAndroid,Version=v6.0"
                }
            };

            // Act
            var packages = await parser.Search("azure +' b ", searchFilter, 0, 1, NullLogger.Instance, CancellationToken.None);
            var package = packages.FirstOrDefault();

            // Assert
            Assert.NotNull((package));
            Assert.Equal("WindowsAzure.Storage", package.Id);
        }
    }
}