# NuGet.Protocol

NuGet.Protocol is a NuGet client SDK library that provides a set of APIs for interacting with NuGet feeds. It provides a way for developers to query NuGet feeds to discover packages and their dependencies, and also to download packages and their associated assets.

## Getting started

NuGet.Protocol can be installed from the NuGet Package Manager or using the dotnet CLI:

```
dotnet add package NuGet.Protocol
```

## Usage

At the center of this library are the PackageSource and SourceRepository types, which represent a NuGet source that may be a file source or an http based source implementing the V2 or [V3](https://learn.microsoft.com/en-us/nuget/api/overview#versioning) protocol.

```
PackageSource localSource = new PackageSource("D:\LocalSource");
SourceRepository localRepository = Repository.Factory.GetCoreV3(localSource);

SourceRepository repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
```

The SourceRepository then has a GetResourceAsync method that you can use to acquire implementations of INuGetResource that often correlate to resources on V3.

```
FindPackageByIdResource resource = await repository.GetResourceAsync<FindPackageByIdResource>(); 
```

## Examples
### List package versions

Find all versions of Newtonsoft.Json using the [NuGet V3 Package Content API](https://learn.microsoft.com/nuget/api/package-base-address-resource#enumerate-package-versions):

```c#
SourceRepository repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
FindPackageByIdResource resource = await repository.GetResourceAsync<FindPackageByIdResource>();

IEnumerable<NuGetVersion> versions = await resource.GetAllVersionsAsync(
    "Newtonsoft.Json",
    new SourceCacheContext(),
    NullLogger.Instance,
    CancellationToken.None);

foreach (NuGetVersion version in versions)
{
    Console.WriteLine($"Found version {version}");
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

### Get package metadata

Get the metadata for the "Newtonsoft.Json" package using the [NuGet V3 Package Metadata API](https://learn.microsoft.com/nuget/api/registration-base-url-resource):

```c#
SourceRepository repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
PackageMetadataResource resource = await repository.GetResourceAsync<PackageMetadataResource>();

IEnumerable<IPackageSearchMetadata> packages = await resource.GetMetadataAsync(
    "Newtonsoft.Json",
    includePrerelease: true,
    includeUnlisted: false,
    new SourceCacheContext(),
    NullLogger.Instance,
    CancellationToken.None);

foreach (IPackageSearchMetadata package in packages)
{
    Console.WriteLine($"Version: {package.Identity.Version}");
    Console.WriteLine($"Listed: {package.IsListed}");
    Console.WriteLine($"Tags: {package.Tags}");
    Console.WriteLine($"Description: {package.Description}");
}
```

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

### Work with authenticated feeds

Use [NuGet.Protocol](https://www.nuget.org/packages/NuGet.Protocol) to work with authenticated feeds.

```c#
var sourceUri = "https://contoso.privatefeed/v3/index.json";
var packageSource = new PackageSource(sourceUri)
{
    Credentials = new PackageSourceCredential(
        source: sourceUri,
        username: "myUsername",
        passwordText: "myVerySecretPassword",
        isPasswordClearText: true,
        validAuthenticationTypesText: null)
};

// If the `SourceRepository` is created with a `PackageSource`, the rest of APIs will consume the credentials attached to `PackageSource.Credentials`.
SourceRepository repository = Repository.Factory.GetCoreV3(packageSource);
PackageMetadataResource resource = await repository.GetResourceAsync<PackageMetadataResource>();

IEnumerable<IPackageSearchMetadata> packages = await resource.GetMetadataAsync(
    "MyPackage",
    includePrerelease: true,
    includeUnlisted: false,
    new SourceCacheContext(),
    NullLogger.Instance,
    CancellationToken.None);

foreach (IPackageSearchMetadata package in packages)
{
    Console.WriteLine($"Version: {package.Identity.Version}");
    Console.WriteLine($"Listed: {package.IsListed}");
    Console.WriteLine($"Tags: {package.Tags}");
    Console.WriteLine($"Description: {package.Description}");
}
```

## Aditional documentation

More information about the NuGet.Protocol library can be found on the [official Microsoft documentation page](https://learn.microsoft.com/nuget/reference/nuget-client-sdk#nugetprotocol).