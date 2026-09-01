# AGENTS.md: NuGet.MSSigning.Extensions

**Scope:** `src\NuGet.Clients\NuGet.MSSigning.Extensions`

## Architecture
- **Commands:** `MSSignCommand` (author signing), `RepoSignCommand` (repo signing)
- **Base:** `MSSignAbstract` — validates X.509 cert fingerprints (SHA-256/384/512), retrieves RSA private key via `CngProvider`
- **Output:** ILMerge'd `NuGet.Mssign.exe` from 34 dependent assemblies (see `ilmerge.props`)
- **Framework:** net472 | **Shipping:** true | **Sign:** Microsoft key + delay-sign

## High-Risk Invariants
- **CNG Provider constraint** (MSSignAbstract.cs): `CngKey.Open(keyContainer, provider, CngKeyOpenOptions.MachineKey)` requires Windows registry/HSM. Cross-platform tests must skip.
- **Cert validation** (MSSignAbstract.cs): SHA-1 fingerprints rejected; deduction logic skips SHA-1 branch only for Thumbprint match.
- **ILMerge post-build** (NuGet.MSSigning.Extensions.csproj): Runs only when `BuildingInsideVisualStudio != 'true'`. SignWithMicrosoftKey enforced via delay-sign keyfile.

## Matching Tests
| Type | Project Path | Entry Tests |
|------|--------------|-------------|
| Unit | `test\NuGet.Clients.Tests\NuGet.MSSigning.Extensions.Test` | `NuGetMSSignCommandTest.cs`, `NuGetReposignCommandTest.cs` |
| Functional | `test\NuGet.Clients.FuncTests\NuGet.MSSigning.Extensions.FuncTest` | `MSSignCommandTests.cs`, `ReposignCommandTests.cs` |

## Validation Commands
```batch
:: Build extension DLL and ILMerge executable
dotnet build src\NuGet.Clients\NuGet.MSSigning.Extensions\NuGet.MSSigning.Extensions.csproj

:: Unit tests (cross-platform compatible)
dotnet test test\NuGet.Clients.Tests\NuGet.MSSigning.Extensions.Test --logger:console

:: Functional tests (Windows + admin required; modifies LocalMachine\Root cert store)
dotnet test test\NuGet.Clients.FuncTests\NuGet.MSSigning.Extensions.FuncTest --logger:console

:: Verify ILMerge artifact
powershell -NoProfile -Command "Test-Path artifacts\VSIX\NuGet.Mssign.exe"
```
