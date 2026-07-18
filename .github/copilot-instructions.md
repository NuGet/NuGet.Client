# NuGet.Client Copilot instructions

NuGet.Client is the .NET codebase for NuGet's client tooling and libraries across Visual Studio, the .NET CLI, MSBuild, and `nuget.exe`.

## Development environment

- Run `.\configure.ps1` on Windows or `./configure.sh` on Linux and macOS to configure the environment before building the repository for the first time.
- Build and test using the repo-local SDK at `.\cli\dotnet.exe` (Windows) or `./cli/dotnet` (Linux/macOS), not a globally installed `dotnet` on `PATH`. For example: `.\cli\dotnet.exe build NuGet.sln` or `.\cli\dotnet.exe test <project>`. Prefer building/testing a specific project over the whole solution when iterating on a single area.
- Do not use `build.ps1`/`build.sh` for routine dev-loop builds — those scripts are for CI/full-restore orchestration, not everyday incremental builds.
- Use NuGet Central Package Management: declare package versions in `Directory.Packages.props` and add versionless `PackageReference` items to project files. Read the [package-update guidance](../docs/updating-packages.md) before changing dependency versions.

## Task-specific guidance

Before implementing code changes, identify and read the guidance relevant to the task. In particular:

- For new features and behavior changes, read the [feature guide](../docs/feature-guide.md), including its requirements for feature configuration, `SdkAnalysisLevel` gating, and restore and pack considerations.
- For public API additions, changes, removals, or shipping, read the [NuGet SDK guidance](../docs/nuget-sdk.md).
- For C# implementation and error-handling patterns, read the [C# conventions and guidelines](agent_docs/csharp.md) and its linked coding guidelines.
- For localized resources, read the [localization guidance](agent_docs/localization.md).
- For nullable migrations, read the [nullable migration guidance](agent_docs/nullable-migrations.md).
- For performance measurements, read the [benchmarking guidance](agent_docs/benchmarking.md).
- Before creating a branch or pull request, read the [Git and pull request guidance](agent_docs/git-workflow.md).

The repository's broader development documentation is indexed in [CONTRIBUTING.md](../CONTRIBUTING.md).