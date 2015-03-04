using NuGet.Protocol.Core.Types;
using System;
using System.Threading.Tasks;
using Xunit;
using NuGet.Protocol.PowerShellGet;
using NuGet.Protocol.Core.v3;
using System.Collections.Generic;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using System.Threading;
using Test.Utility;
using System.Linq;

namespace PowerShellGet.Tests
{
    public class SearchTests
    {
        [Fact]
        public async Task PowerShellSearchResource_WithPSMetadata()
        {
            Dictionary<string, string> responses = new Dictionary<string, string>();
            responses.Add("http://testsource.com/v3/index.json", JsonData.IndexJson);
            responses.Add("https://api-v3search-0.nuget.org/query?q=test&skip=0&take=10&includePrerelease=false", JsonData.ExamplePSMetadata);

            var repo = StaticHttpHandler.CreateSource("http://testsource.com/v3/index.json", Repository.Provider.GetPowerShell(), responses);

            var resource = await repo.GetResourceAsync<PowerShellSearchResource>();

            var results = await resource.Search("test", new SearchFilter(), 0, 10, CancellationToken.None);

            var package = results.FirstOrDefault();

            // verify the identity is used everywhere
            Assert.Equal("EntityFramework", package.Id);
            Assert.Equal("6.1.2", package.Version.ToNormalizedString());

            Assert.Equal("EntityFramework", package.ServerPackage.Id);
            Assert.Equal("6.1.2", package.ServerPackage.Version.ToNormalizedString());

            Assert.Equal("EntityFramework", package.PowerShellMetadata.Id);
            Assert.Equal("6.1.2", package.PowerShellMetadata.Version.ToNormalizedString());

            Assert.Equal("http://go.microsoft.com/fwlink/?LinkID=386613", package.ServerPackage.IconUrl.AbsoluteUri);

            // verify the PS metadata
            Assert.Equal("1.0.0", package.PowerShellMetadata.ModuleVersion.ToNormalizedString());
            Assert.Equal("Microsoft Corporation", package.PowerShellMetadata.CompanyName);
            Assert.Equal("e4da48d8-20df-4d58-bfa6-2e54486fca5b", package.PowerShellMetadata.Guid.ToString());
            Assert.Equal("5.0.0", package.PowerShellMetadata.PowerShellHostVersion.ToNormalizedString());
            Assert.Equal("4.0.0", package.PowerShellMetadata.DotNetFrameworkVersion.ToNormalizedString());
            Assert.Equal("4.0.0", package.PowerShellMetadata.CLRVersion.ToNormalizedString());
            Assert.Equal("x64, x86", package.PowerShellMetadata.ProcessorArchitecture);
            Assert.Equal("Get-Test|Get-Test2", String.Join("|", package.PowerShellMetadata.CmdletsToExport));
            Assert.Equal("Set-Test", String.Join("|", package.PowerShellMetadata.FunctionsToExport));
            Assert.Equal("xFirefox", String.Join("|", package.PowerShellMetadata.DscResourcesToExport));
            Assert.Equal(new Uri("http://license.com"), package.PowerShellMetadata.LicenseUrl);
            Assert.Equal(new Uri("http://project.com"), package.PowerShellMetadata.ProjectUrl);
            Assert.Equal("http://release.notes.com", package.PowerShellMetadata.ReleaseNotes);
        }

        [Fact]
        public async Task PowerShellSearchResource_WithoutPSMetadata()
        {
            Dictionary<string, string> responses = new Dictionary<string, string>();
            responses.Add("http://testsource.com/v3/index.json", JsonData.IndexJson);
            responses.Add("https://api-v3search-0.nuget.org/query?q=test&skip=0&take=10&includePrerelease=false", JsonData.NonPSMetadata);

            var repo = StaticHttpHandler.CreateSource("http://testsource.com/v3/index.json", Repository.Provider.GetPowerShell(), responses);

            var resource = await repo.GetResourceAsync<PowerShellSearchResource>();

            var results = await resource.Search("test", new SearchFilter(), 0, 10, CancellationToken.None);

            var package = results.FirstOrDefault();

            // verify the identity is used everywhere
            Assert.Equal("EntityFramework", package.Id);
            Assert.Equal("6.1.2", package.Version.ToNormalizedString());

            Assert.Equal("EntityFramework", package.ServerPackage.Id);
            Assert.Equal("6.1.2", package.ServerPackage.Version.ToNormalizedString());

            Assert.Equal("EntityFramework", package.PowerShellMetadata.Id);
            Assert.Equal("6.1.2", package.PowerShellMetadata.Version.ToNormalizedString());

            Assert.Equal("http://go.microsoft.com/fwlink/?LinkID=386613", package.ServerPackage.IconUrl.AbsoluteUri);

            // verify the PS metadata is empty
            Assert.Null(package.PowerShellMetadata.ModuleVersion);
        }
    }
}
