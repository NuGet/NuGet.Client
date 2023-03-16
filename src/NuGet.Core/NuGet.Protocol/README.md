# NuGet.Protocol

NuGet.Protocol is a NuGet client SDK library that provides a set of APIs for interacting with NuGet feeds. It provides a way for developers to query NuGet feeds to discover packages and their dependencies, and also to download packages and their associated assets.

## Getting started

NuGet.Protocol can be installed from the NuGet Package Manager or using the dotnet CLI:

```
dotnet add package NuGet.Protocol
```

## Usage

At the center of this library are the PackageSource and SourceRepository types, which represent a NuGet source that may be a file source or an http based source implementing the V2 or [V3](https://learn.microsoft.com/nuget/api/overview#versioning) protocol.

```
PackageSource localSource = new PackageSource("D:\LocalSource");
SourceRepository localRepository = Repository.Factory.GetCoreV3(localSource);

SourceRepository repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
```

The SourceRepository then has a GetResourceAsync method that you can use to acquire implementations of INuGetResource that often are [V3](https://learn.microsoft.com/nuget/api/overview#versioning) resources.

```
FindPackageByIdResource resource = await repository.GetResourceAsync<FindPackageByIdResource>(); 
```

## Examples
### Search packages

Search for "json" packages using the [NuGet V3 Search API](https://learn.microsoft.com/nuget/api/search-query-service-resource):

```c#
SourceRepository repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
PackageSearchResource resource = await repository.GetResourceAsync<PackageSearchResource>();
SearchFilter searchFilter = new SearchFilter(includePrerelease: true);

IEnumerable<IPackageSearchMetadata> results = await resource.SearchAsync(
    "json",
    searchFilter,
    skip: 0,
    take: 20,
    NullLogger.Instance,
    CancellationToken.None);

foreach (IPackageSearchMetadata result in results)
{
    Console.WriteLine($"Found package {result.Identity.Id} {result.Identity.Version}");
}
```

### Download a package

Download Newtonsoft.Json v12.0.1 using the [NuGet V3 Package Content API](https://learn.microsoft.com/nuget/api/package-base-address-resource):

```c#
SourceRepository repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
FindPackageByIdResource resource = await repository.GetResourceAsync<FindPackageByIdResource>();

string packageId = "Newtonsoft.Json";
NuGetVersion packageVersion = new NuGetVersion("12.0.1");
using MemoryStream packageStream = new MemoryStream();

await resource.CopyNupkgToStreamAsync(
    packageId,
    packageVersion,
    packageStream,
    new SourceCacheContext(),
    NullLogger.Instance,
    CancellationToken.None);

Console.WriteLine($"Downloaded package {packageId} {packageVersion}");

using PackageArchiveReader packageReader = new PackageArchiveReader(packageStream);
NuspecReader nuspecReader = await packageReader.GetNuspecReaderAsync(cancellationToken);

Console.WriteLine($"Tags: {nuspecReader.GetTags()}");
Console.WriteLine($"Description: {nuspecReader.GetDescription()}");
```

### Push a package

Push a package using the [NuGet V3 Push and Delete API](https://learn.microsoft.com/nuget/api/package-publish-resource):

```c#
SourceRepository repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
PackageUpdateResource resource = await repository.GetResourceAsync<PackageUpdateResource>();

string apiKey = "my-api-key";

await resource.Push(
    "MyPackage.nupkg",
    symbolSource: null,
    timeoutInSecond: 5 * 60,
    disableBuffering: false,
    getApiKey: packageSource => apiKey,
    getSymbolApiKey: packageSource => null,
    noServiceEndpoint: false,
    skipDuplicate: false,
    symbolPackageUpdateResource: null,
    NullLogger.Instance);
```

## Aditional documentation

You can browse these and more samples on our [NuGet Client SDK documentation](https://docs.microsoft.com/nuget/reference/nuget-client-sdk).

More information about the NuGet.Protocol library can be found on the [official Microsoft documentation page](https://learn.microsoft.com/nuget/reference/nuget-client-sdk#nugetprotocol) and [NuGet API docs](https://learn.microsoft.com/nuget/api/overview).