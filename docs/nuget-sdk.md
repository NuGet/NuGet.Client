# NuGet SDK

NuGet is primarily a tool, but we also publish packages that others can reference in their own projects. All of these packages are published to NuGet.org with the [`NuGet.` prefix](https://www.nuget.org/packages?q=nuget.*). Documentation on the NuGet SDK is on our docs site [https://docs.microsoft.com/nuget/reference/nuget-client-sdk](https://docs.microsoft.com/nuget/reference/nuget-client-sdk)

The projects that are part of the SDK are largely under `src\NuGet.Core`, but also include `NuGet.VisualStudio` and `NuGet.VisualStudio.Contracts` under `src\NuGet.Clients`, which define the interfaces for our Visual Studio extensibility APIs.

## API compatibility policy

NuGet is primarily a tool, and as such our efforts are focused on: Visual Studio integration, `dotnet` cli integration, and `msbuild` integration.

When we need to break APIs part of the SDK to meet our tooling needs, we will do so. However, we understand this negatively impacts customers using the SDK, so we will try to avoid doing so by trying to be attentive in code reviews, and using tooling such as the [PublicApiAnalyzers](https://github.com/dotnet/roslyn-analyzers/tree/master/src/PublicApiAnalyzers).

## Changes to APIs

While the NuGet team does not have an API design process, if you contribute changes to any projects that make up the NuGet SDK try to make a best effort. For example, consider:

* If a customer is referencing the assembly you are modifying, does the API make sense without the context of the full PR you are creating?
* Is the API within the scope of the assembly? For example, APIs related to `nuget.config` files belong only in the `NuGet.Configuration` assembly. `NuGet.Protocol` is for APIs related to querying and downloading packages from package sources. Just because it uses HTTP and JSON does not mean that it should provide helper utilities for `HttpClient` or JSON conversion.
* If you are modifying the SDK as part of a change for a project that is not part of the SDK, does the API you are creating belong in the other project?
* Do all of your public APIs need to be public, or can they be made internal?
* If there was an API design review panel, and you were a member of it, if someone else proposed the API you're adding, would you approve it?
* Is the API a little bit too specific for the scenario you're working on, and could the API be made a little bit more generic to provide value to customers with slightly different use-cases?
* Does the change make it possible for classes to be put in an invalid state if not used correctly?
* Can any classes being added or modified have multithreading issues?

Ideally once an API is part of the SDK, it is "set in stone" and can "never" change. We have no way to determine if anyone is using an API, or how commonly different APIs are being used. Therefore the barrier to adding new APIs should be high

## PublicApiAnalyzer

The NuGet.Client repo now uses [PublicApiAnalyzers](https://github.com/dotnet/roslyn-analyzers/tree/master/src/PublicApiAnalyzers) on the projects that make up the NuGet SDK. This helps the team detect changes, and is a signal to the developer making the change to consider the design recommendations above, and a signal to code reviews to consider the same recommendations.

Using the PublicApiAnalyzers changes the NuGet development and release lifecycle in the following ways:

### Development

Each project in the NuGet SDK will have a `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt` file, as required by the analyzer. Most NuGet projects (possibly all NuGet SDK projects) multitarget, and when a project has the same public API surface for all Target Frameworks (TFMs), they will use a single pair of files in the project root. When a project has different public API surface, each TFM will have their own pair of files in the `PublicAPI\<tfm>\` folder in the project.

When an API is added, the analyzer will display errors unless the API is added to `PublicAPI.Unshipped.txt`. Note:
* The analyzer will also stop complaining if the API is added to `PublicAPI.Shipped.txt`. However, new APIs should not be added to this file. Always add new APIs to the `Unshipped` file.
* Adding APIs to the text file can be accomplished by using the analyzer's code-fix from Visual Studio. Developers not using Visual Studio (for example VSCode) may need to find alternative methods.
  * The analyzer code-fix only adds the API to the currently selected TFM's `PublicAPI.Unshipped.txt` file. Developers will need to use the drop down menu

## Code Review

Unless a PR is specifically for the intent of moving APIs from `PublicAPI.Unshipped.txt` to `PublicAPI.Shipped.txt` because a new version of NuGet was shipped, any PR adding APIs to `PublicAPI.Shipped.txt` should be questioned.

Any PR removing APIs from `PublicAPI.Shipped.txt` shoud be seriously questioned. Usually overloads should be used to prevent

