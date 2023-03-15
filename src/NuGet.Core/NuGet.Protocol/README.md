# NuGet.Protocol

NuGet.Protocol is a NuGet client SDK library that provides a set of APIs for interacting with NuGet feeds. It provides a way for developers to query NuGet feeds to discover packages and their dependencies, and also to download packages and their associated assets.

# Usage

## List package versions

Find all versions of Newtonsoft.Json using the [NuGet V3 Package Content API](https://learn.microsoft.com/nuget/api/package-base-address-resource#enumerate-package-versions):

```c#
ILogger logger = NullLogger.Instance;
CancellationToken cancellationToken = CancellationToken.None;

SourceCacheContext cache = new SourceCacheContext();
SourceRepository repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
FindPackageByIdResource resource = await repository.GetResourceAsync<FindPackageByIdResource>();

IEnumerable<NuGetVersion> versions = await resource.GetAllVersionsAsync(
    "Newtonsoft.Json",
    cache,
    logger,
    cancellationToken);

foreach (NuGetVersion version in versions)
{
    Console.WriteLine($"Found version {version}");
}
```

### GetAllVersionsAsync(id,cacheContext,logger,cancellationToken) `method`

##### Summary

Asynchronously gets all package versions for a package ID.

##### Returns

A task that represents the asynchronous operation.
The task result ([Result](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1.result?view=net-7.0#system-threading-tasks-task-1-result 'System.Threading.Tasks.Task`1.Result')) returns an
[IEnumerable\`1](https://learn.microsoft.com/dotnet/api/system.collections.generic.ienumerable-1?view=net-7.0 'System.Collections.Generic.IEnumerable`1').

##### Parameters

| Name | Type | Description |
| ---- | ---- | ----------- |
| id | System.String| A package ID. |
| cacheContext | NuGet.Protocol.Core.Types.SourceCacheContext | A source cache context. |
| logger | NuGet.Common.ILogger | A logger. |
| cancellationToken | System.Threading.CancellationToken | A cancellation token. |

##### Exceptions

| Name | Description |
| ---- | ----------- |
| [System.ArgumentException](https://learn.microsoft.com/dotnet/api/system.argumentexception?view=net-7.0 'System.ArgumentException') | Thrown if `id` is either `null` or an empty string. |
| [System.ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception?view=net-7.0 'System.ArgumentNullException') | Thrown if `cacheContext` `null`. |
| [System.ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception?view=net-7.0 'System.ArgumentNullException') | Thrown if `logger` `null`. |
| [System.OperationCanceledException](https://learn.microsoft.com/dotnet/api/system.operationcanceledexception?view=net-7.0 'System.OperationCanceledException') | Thrown if `cancellationToken` is cancelled. |
### Download a package

Download Newtonsoft.Json v12.0.1 using the [NuGet V3 Package Content API](https://learn.microsoft.com/nuget/api/package-base-address-resource):

```c#
ILogger logger = NullLogger.Instance;
CancellationToken cancellationToken = CancellationToken.None;

SourceCacheContext cache = new SourceCacheContext();
SourceRepository repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
FindPackageByIdResource resource = await repository.GetResourceAsync<FindPackageByIdResource>();

string packageId = "Newtonsoft.Json";
NuGetVersion packageVersion = new NuGetVersion("12.0.1");
using MemoryStream packageStream = new MemoryStream();

await resource.CopyNupkgToStreamAsync(
    packageId,
    packageVersion,
    packageStream,
    cache,
    logger,
    cancellationToken);

Console.WriteLine($"Downloaded package {packageId} {packageVersion}");

using PackageArchiveReader packageReader = new PackageArchiveReader(packageStream);
NuspecReader nuspecReader = await packageReader.GetNuspecReaderAsync(cancellationToken);

Console.WriteLine($"Tags: {nuspecReader.GetTags()}");
Console.WriteLine($"Description: {nuspecReader.GetDescription()}");
```

## Get package metadata

Get the metadata for the "Newtonsoft.Json" package using the [NuGet V3 Package Metadata API](https://learn.microsoft.com/nuget/api/registration-base-url-resource):

```c#
ILogger logger = NullLogger.Instance;
CancellationToken cancellationToken = CancellationToken.None;

SourceCacheContext cache = new SourceCacheContext();
SourceRepository repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
PackageMetadataResource resource = await repository.GetResourceAsync<PackageMetadataResource>();

IEnumerable<IPackageSearchMetadata> packages = await resource.GetMetadataAsync(
    "Newtonsoft.Json",
    includePrerelease: true,
    includeUnlisted: false,
    cache,
    logger,
    cancellationToken);

foreach (IPackageSearchMetadata package in packages)
{
    Console.WriteLine($"Version: {package.Identity.Version}");
    Console.WriteLine($"Listed: {package.IsListed}");
    Console.WriteLine($"Tags: {package.Tags}");
    Console.WriteLine($"Description: {package.Description}");
}
```

## Search packages

Search for "json" packages using the [NuGet V3 Search API](https://learn.microsoft.com/nuget/api/search-query-service-resource):

```c#
ILogger logger = NullLogger.Instance;
CancellationToken cancellationToken = CancellationToken.None;

SourceRepository repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
PackageSearchResource resource = await repository.GetResourceAsync<PackageSearchResource>();
SearchFilter searchFilter = new SearchFilter(includePrerelease: true);

IEnumerable<IPackageSearchMetadata> results = await resource.SearchAsync(
    "json",
    searchFilter,
    skip: 0,
    take: 20,
    logger,
    cancellationToken);

foreach (IPackageSearchMetadata result in results)
{
    Console.WriteLine($"Found package {result.Identity.Id} {result.Identity.Version}");
}
```

## Push a package

Push a package using the [NuGet V3 Push and Delete API](https://learn.microsoft.com/nuget/api/package-publish-resource):

```c#
ILogger logger = NullLogger.Instance;
CancellationToken cancellationToken = CancellationToken.None;

SourceCacheContext cache = new SourceCacheContext();
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
    logger);
```

## Delete a package

Delete a package using the [NuGet V3 Push and Delete API](https://learn.microsoft.com/nuget/api/package-publish-resource):

```c#
ILogger logger = NullLogger.Instance;
CancellationToken cancellationToken = CancellationToken.None;

SourceCacheContext cache = new SourceCacheContext();
SourceRepository repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
PackageUpdateResource resource = await repository.GetResourceAsync<PackageUpdateResource>();

string apiKey = "my-api-key";

await resource.Delete(
    "MyPackage",
    "1.0.0-beta",
    getApiKey: packageSource => apiKey,
    confirm: packageSource => true,
    noServiceEndpoint: false,
    logger);
```

## Work with authenticated feeds

Use [NuGet.Protocol](https://www.nuget.org/packages/NuGet.Protocol) to work with authenticated feeds.

```c#
ILogger logger = NullLogger.Instance;
CancellationToken cancellationToken = CancellationToken.None;
SourceCacheContext cache = new SourceCacheContext();
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
    cache,
    logger,
    cancellationToken);

foreach (IPackageSearchMetadata package in packages)
{
    Console.WriteLine($"Version: {package.Identity.Version}");
    Console.WriteLine($"Listed: {package.IsListed}");
    Console.WriteLine($"Tags: {package.Tags}");
    Console.WriteLine($"Description: {package.Description}");
}
```

# Aditional documentation

More information about the NuGet.Protocol library can be found on the [official Microsoft documentation page](https://learn.microsoft.com/nuget/reference/nuget-client-sdk#nugetprotocol).