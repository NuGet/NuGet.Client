# Copilot instructions

## Build, test, format
- First-time setup (required at least once): `.\configure.ps1`
- Build (Debug by default): `.\build.ps1`
- Build + unit tests: `.\build.ps1 -RunUnitTests`
- Full unit + functional tests: `.\runTests.ps1`
- Run a single test:
  - `dotnet vstest bin\debug\netcoreapp5.0\<TestProject>.dll /Tests:<TestFilterHere>`
  - Apex tests: `dotnet test .\test\NuGet.Tests.Apex\NuGet.Tests.Apex\bin\Debug\NuGet.Tests.Apex.dll --filter <FullyQualifiedTestName>`
- Format/whitespace check: `dotnet format whitespace --verify-no-changes NuGet.sln`
- Always format before submitting a pull request.

## Architecture (high level)
- Single solution: `NuGet.sln` (use `*.slnf` filters like `NuGet-UnitTests.slnf` for subsets).
- `src\NuGet.Core`: shared libraries and MSBuild tasks (restore/pack, protocol, packaging, versioning).
- `src\NuGet.Clients`: user-facing clients (NuGet.exe CLI, dotnet nuget via XPlat, VS extension UI/PMC/cmdlets).
- Visual Studio extension entry points live in `NuGet.Tools` (UI) and `NuGet.SolutionRestoreManager` (restore).
- `build\` + `scripts\`: build orchestration driven by `build\build.proj`; outputs in `artifacts\VS15` and `artifacts\nupkgs`.
- `test\`: unit tests, functional tests, and VS Apex end-to-end tests (`test\NuGet.Tests.Apex`).

## Key conventions
- Follow the coding guidelines: https://github.com/NuGet/NuGet.Client/blob/dev/docs/coding-guidelines.md
- New source files must include the standard copyright header.
- Do not use reflection.
- Avoid `as` casts; use direct cast or pattern matching checks.
- Use `IEnvironmentVariableReader`/`EnvironmentVariableWrapper` for env vars; avoid setting env vars.
- Test naming: classes end with `Test`, methods `Thing_Condition_Expectation`; use Arrange/Act/Assert with a single Act statement.
- Project naming pattern: `NuGet.<area>.<subarea>`; test assemblies end with `.Tests` or `.FuncTests`.
- When creating pull requests, always follow `.github\PULL_REQUEST_TEMPLATE.md`.
