# NuGet.VisualStudio.Contracts

The package NuGet.VisualStudio.Contracts contains RPC contracts for NuGet’s Visual Studio Service Broker extensibility APIs. These APIs are designed to be usable with async code and are available in this package using Visual Studio’s `IServiceBroker`.

## Usage

Install the NuGet.VisualStudio.Contracts package into your project, as well as [Microsoft.VisualStudio.SDK](https://www.nuget.org/packages/Microsoft.VisualStudio.SDK).

Use the IAsyncServiceProvider to get Visual Studio's service broker, and use that to get NuGet's service. Note that [AsyncPackage extends IVsAsyncServiceProvider2](https://learn.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.shell.asyncpackage?view=visualstudiosdk-2022), so your class that implements AsyncPackage can be used as the IAsyncServiceProvider. Also see the docs on [IBrokeredServiceContainer](https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.shell.servicebroker.ibrokeredservicecontainer?view=visualstudiosdk-2022) and [IServiceBroker](https://learn.microsoft.com/dotnet/api/microsoft.servicehub.framework.iservicebroker?view=visualstudiosdk-2022)

```c#
// Your AsyncPackage implements IAsyncServiceProvider
IAsyncServiceProvider asyncServiceProvider = this;
var brokeredServiceContainer = await asyncServiceProvider.GetServiceAsync<SVsBrokeredServiceContainer, IBrokeredServiceContainer>();
var serviceBroker = brokeredServiceContainer.GetFullAccessServiceBroker();
var nugetProjectService = await serviceBroker.GetProxyAsync<INuGetProjectService>(NuGetServices.NuGetProjectServiceV1);
```

## Example

### Get installed packages of a project.

```c#
InstalledPackagesResult installedPackagesResult;
using (nugetProjectService as IDisposable)
{
    installedPackagesResult = await nugetProjectService.GetInstalledPackages(projectGuid, cancellationToken);
}
```

## Aditional documentation

More information about the NuGet.Protocol library can be found on the [official Microsoft documentation page](https://learn.microsoft.com/nuget/reference/nuget-client-sdk#nugetprotocol) and [NuGet API docs](https://learn.microsoft.com/nuget/visual-studio-extensibility/nuget-api-in-visual-studio).