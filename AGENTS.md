# NuGet.Client agent instructions

NuGet.Client is the .NET codebase for NuGet tooling and libraries used by
Visual Studio, the .NET CLI, MSBuild, and `nuget.exe`.

## Scope and precedence

- These instructions apply to the entire repository.
- Read and follow the nearest nested `AGENTS.md` for the files being changed.
- Preserve user changes and do not modify unrelated files.
- Prefer the smallest complete change that addresses the root cause.

## Before making changes

1. Inspect the affected projects and tests, and search for existing patterns
   before adding new helpers or abstractions.
2. Read the guidance relevant to the task:

   | Change | Required guidance |
   | --- | --- |
   | New feature or behavior change | [`docs/feature-guide.md`](docs/feature-guide.md) |
   | Public API change | [`docs/nuget-sdk.md`](docs/nuget-sdk.md) |
   | C# implementation | [`.github/agent_docs/csharp.md`](.github/agent_docs/csharp.md) |
   | Localization | [`.github/agent_docs/localization.md`](.github/agent_docs/localization.md) |
   | Nullable migration | [`.github/agent_docs/nullable-migrations.md`](.github/agent_docs/nullable-migrations.md) |
   | Performance measurement | [`.github/agent_docs/benchmarking.md`](.github/agent_docs/benchmarking.md) |
   | Dependency update | [`docs/updating-packages.md`](docs/updating-packages.md) |
   | Branch or pull request | [`.github/agent_docs/git-workflow.md`](.github/agent_docs/git-workflow.md) |

3. Use [`CONTRIBUTING.md`](CONTRIBUTING.md) as the index for broader
   development documentation.

## Implementation rules

- Follow existing project conventions and reuse established test utilities.
- Add or update tests when behavior changes.
- Consider all affected NuGet surfaces: Visual Studio, `dotnet`, MSBuild,
  `nuget.exe`, restore, and pack. Change only the surfaces relevant to the task.
- Follow `SdkAnalysisLevel` and feature-configuration requirements for new
  behavior.
- Keep public API files accurate when changing public surface area.
- Declare package versions in `Directory.Packages.props`; project
  `PackageReference` items must be versionless.
- Do not edit generated localization `.xlf` files manually. Edit the `.resx`
  and build the owning project to regenerate dependent files.

## Build and test

- Configure once before the first build:
  - Windows: `.\configure.ps1`
  - Linux or macOS: `. ./configure.sh`
- Build only the projects relevant to the change with `dotnet` or `msbuild`.
- On Linux and macOS, do not build `NuGet.sln`; it contains Windows-only
  projects. Use `./build.sh` when a broader cross-platform build is needed.
- Run the smallest relevant tests with `dotnet test`, using `--filter` when
  possible.
- Before submitting a pull request, run:
  `dotnet format whitespace --verify-no-changes NuGet.sln`

## Completion

- Ensure source, tests, public API files, generated artifacts, and directly
  related documentation remain consistent.
- Report the files changed and the exact validation performed.
