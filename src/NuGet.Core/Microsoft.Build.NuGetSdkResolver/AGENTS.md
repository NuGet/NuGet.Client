# Microsoft.Build.NuGetSdkResolver

## High-Risk Invariants

1. **Async Restore Mandatory**: SDK resolution runs Task.Run(() => RestoreRunnerEx.RunWithoutCommit()) to prevent UI thread deadlock on legacy VS project evaluation (async required, not optional).

2. **Framework-Conditional Signing**: X509TrustStore certificate loading only on CoreCLR (#if !NETFRAMEWORK); omitted on .NET Framework 4.7.2 to avoid runtime errors.

3. **Case-Sensitive Path Handling**: Resolver accepts both "Sdk" and "sdk" folder names in package root; critical on non-Windows filesystems.

4. **Lazy NuGet Assembly Loading**: Core resolver avoids direct NuGet class references in public Resolve() method; NuGet assemblies loaded only in private NuGetAbstraction class to minimize startup impact.

5. **Thread-Safe Global.json Cache**: GlobalJsonReader uses ConcurrentDictionary with FileSystemInfoFullNameEqualityComparer for path comparison; cache invalidated on file modification.

## Target Frameworks

- **net472** (when IsVsixBuild==true) or included in net472;net10.0 (normal build)
- **net10.0** or **net8.0** (CoreCLR variant)
- MSBuild.Framework dependency compiled-only, runtime excluded

## Test Coverage

Matching test project: test\NuGet.Core.Tests\Microsoft.Build.NuGetSdkResolver.Test\

## Validation

### Build
```powershell
dotnet build src\NuGet.Core\Microsoft.Build.NuGetSdkResolver\Microsoft.Build.NuGetSdkResolver.csproj -c Release
```

### Test
```powershell
dotnet test test\NuGet.Core.Tests\Microsoft.Build.NuGetSdkResolver.Test\Microsoft.Build.NuGetSdkResolver.Test.csproj -c Release --filter "FullyQualifiedName~NuGetSdkResolverTests"
```

## Owned Paths

- src\NuGet.Core\Microsoft.Build.NuGetSdkResolver\**
- test\NuGet.Core.Tests\Microsoft.Build.NuGetSdkResolver.Test\**
