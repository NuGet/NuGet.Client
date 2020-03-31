---
date-generated: 2020-03-30T16:36:02
tool: NuGetTasks.GenerateMarkdownDoc
---


# NuGet Project Overview

Below is a list of all source code projects for NuGet libraries and supported NuGet clients



## Projects

All shipped NuGet libraries and clients lives in `src/` folder.

Projects count: 37

- [`src\NuGet.Clients\NuGet.CommandLine\NuGet.CommandLine.csproj`](../src/NuGet.Clients/NuGet.CommandLine/NuGet.CommandLine.csproj): NuGet Command Line Interface.
- [`src\NuGet.Clients\NuGet.Console\NuGet.Console.csproj`](../src/NuGet.Clients/NuGet.Console/NuGet.Console.csproj): Package Manager PowerShell Console implementation.
- [`src\NuGet.Clients\NuGet.MSSigning.Extensions\NuGet.MSSigning.Extensions.csproj`](../src/NuGet.Clients/NuGet.MSSigning.Extensions/NuGet.MSSigning.Extensions.csproj): NuGet Command Line Interface for repository signing.
- [`src\NuGet.Clients\NuGet.PackageManagement.PowerShellCmdlets\NuGet.PackageManagement.PowerShellCmdlets.csproj`](../src/NuGet.Clients/NuGet.PackageManagement.PowerShellCmdlets/NuGet.PackageManagement.PowerShellCmdlets.csproj): NuGet's PowerShell cmdlets for the Visual Studio client.
- [`src\NuGet.Clients\NuGet.PackageManagement.UI\NuGet.PackageManagement.UI.csproj`](../src/NuGet.Clients/NuGet.PackageManagement.UI/NuGet.PackageManagement.UI.csproj): Package Management UI elements for Visual Studio, Package Manager UI, Migrator, Options dialog.
- [`src\NuGet.Clients\NuGet.PackageManagement.VisualStudio\NuGet.PackageManagement.VisualStudio.csproj`](../src/NuGet.Clients/NuGet.PackageManagement.VisualStudio/NuGet.PackageManagement.VisualStudio.csproj): NuGet's Visual Studio functionalities, integrations and utilities.
- [`src\NuGet.Clients\NuGet.SolutionRestoreManager.Interop\NuGet.SolutionRestoreManager.Interop.csproj`](../src/NuGet.Clients/NuGet.SolutionRestoreManager.Interop/NuGet.SolutionRestoreManager.Interop.csproj): APIs for invoking NuGet Restore Manager in Visual Studio.
- [`src\NuGet.Clients\NuGet.SolutionRestoreManager\NuGet.SolutionRestoreManager.csproj`](../src/NuGet.Clients/NuGet.SolutionRestoreManager/NuGet.SolutionRestoreManager.csproj): NuGet's Visual Studio Solution Restore Manager.
- [`src\NuGet.Clients\NuGet.Tools\NuGet.Tools.csproj`](../src/NuGet.Clients/NuGet.Tools/NuGet.Tools.csproj): NuGet's Visual Studio extension Package.
- [`src\NuGet.Clients\NuGet.VisualStudio.Client\NuGet.VisualStudio.Client.csproj`](../src/NuGet.Clients/NuGet.VisualStudio.Client/NuGet.VisualStudio.Client.csproj): NuGet Visual Studio extension package project.
- [`src\NuGet.Clients\NuGet.VisualStudio.Common\NuGet.VisualStudio.Common.csproj`](../src/NuGet.Clients/NuGet.VisualStudio.Common/NuGet.VisualStudio.Common.csproj): NuGet's Visual Studio common types and interfaces used for both Package Manager UI, Package Manager Console, restore and install functionalities.
- [`src\NuGet.Clients\NuGet.VisualStudio.Implementation\NuGet.VisualStudio.Implementation.csproj`](../src/NuGet.Clients/NuGet.VisualStudio.Implementation/NuGet.VisualStudio.Implementation.csproj): Implementation of the NuGet.VisualStudio extensibility APIs.
- [`src\NuGet.Clients\NuGet.VisualStudio.Interop\NuGet.VisualStudio.Interop.csproj`](../src/NuGet.Clients/NuGet.VisualStudio.Interop/NuGet.VisualStudio.Interop.csproj): NuGet's Visual Studio client Template Wizard interop implementation.
- [`src\NuGet.Clients\NuGet.VisualStudio.OnlineEnvironment.Client\NuGet.VisualStudio.OnlineEnvironment.Client.csproj`](../src/NuGet.Clients/NuGet.VisualStudio.OnlineEnvironment.Client/NuGet.VisualStudio.OnlineEnvironment.Client.csproj): 
- [`src\NuGet.Clients\NuGet.VisualStudio\NuGet.VisualStudio.csproj`](../src/NuGet.Clients/NuGet.VisualStudio/NuGet.VisualStudio.csproj): APIs for invoking NuGet services in Visual Studio.
- [`src\NuGet.Clients\NuGetConsole.Host.PowerShell\NuGetConsole.Host.PowerShell.csproj`](../src/NuGet.Clients/NuGetConsole.Host.PowerShell/NuGetConsole.Host.PowerShell.csproj): Package Manager Console PowerShell host implementation.
- [`src\NuGet.Core\Microsoft.Build.NuGetSdkResolver\Microsoft.Build.NuGetSdkResolver.csproj`](../src/NuGet.Core/Microsoft.Build.NuGetSdkResolver/Microsoft.Build.NuGetSdkResolver.csproj): MSBuild SDK resolver for NuGet packages.
- [`src\NuGet.Core\NuGet.Build.Tasks.Console\NuGet.Build.Tasks.Console.csproj`](../src/NuGet.Core/NuGet.Build.Tasks.Console/NuGet.Build.Tasks.Console.csproj): NuGet Build tasks for MSBuild and dotnet restore. Contains restore logic using the MSBuild static graph functionality.
- [`src\NuGet.Core\NuGet.Build.Tasks.Pack\NuGet.Build.Tasks.Pack.csproj`](../src/NuGet.Core/NuGet.Build.Tasks.Pack/NuGet.Build.Tasks.Pack.csproj): NuGet tasks for MSBuild and dotnet pack.
- [`src\NuGet.Core\NuGet.Build.Tasks\NuGet.Build.Tasks.csproj`](../src/NuGet.Core/NuGet.Build.Tasks/NuGet.Build.Tasks.csproj): NuGet tasks for MSBuild and dotnet restore.
- [`src\NuGet.Core\NuGet.CommandLine.XPlat\NuGet.CommandLine.XPlat.csproj`](../src/NuGet.Core/NuGet.CommandLine.XPlat/NuGet.CommandLine.XPlat.csproj): NuGet executable wrapper for the dotnet CLI nuget functionality.
- [`src\NuGet.Core\NuGet.Commands\NuGet.Commands.csproj`](../src/NuGet.Core/NuGet.Commands/NuGet.Commands.csproj): Complete commands common to command-line and GUI NuGet clients.
- [`src\NuGet.Core\NuGet.Common\NuGet.Common.csproj`](../src/NuGet.Core/NuGet.Common/NuGet.Common.csproj): Common utilities and interfaces for all NuGet libraries.
- [`src\NuGet.Core\NuGet.Configuration\NuGet.Configuration.csproj`](../src/NuGet.Core/NuGet.Configuration/NuGet.Configuration.csproj): NuGet's configuration settings implementation.
- [`src\NuGet.Core\NuGet.Credentials\NuGet.Credentials.csproj`](../src/NuGet.Core/NuGet.Credentials/NuGet.Credentials.csproj): NuGet client's authentication models.
- [`src\NuGet.Core\NuGet.DependencyResolver.Core\NuGet.DependencyResolver.Core.csproj`](../src/NuGet.Core/NuGet.DependencyResolver.Core/NuGet.DependencyResolver.Core.csproj): NuGet's PackageReference dependency resolver implementation.
- [`src\NuGet.Core\NuGet.Frameworks\NuGet.Frameworks.csproj`](../src/NuGet.Core/NuGet.Frameworks/NuGet.Frameworks.csproj): NuGet's understanding of target frameworks.
- [`src\NuGet.Core\NuGet.Indexing\NuGet.Indexing.csproj`](../src/NuGet.Core/NuGet.Indexing/NuGet.Indexing.csproj): NuGet's indexing library for the Visual Studio client search functionality.
- [`src\NuGet.Core\NuGet.LibraryModel\NuGet.LibraryModel.csproj`](../src/NuGet.Core/NuGet.LibraryModel/NuGet.LibraryModel.csproj): NuGet's types and interfaces for understanding dependencies.
- [`src\NuGet.Core\NuGet.Localization\NuGet.Localization.csproj`](../src/NuGet.Core/NuGet.Localization/NuGet.Localization.csproj): NuGet localization package for dotnet CLI.
- [`src\NuGet.Core\NuGet.PackageManagement\NuGet.PackageManagement.csproj`](../src/NuGet.Core/NuGet.PackageManagement/NuGet.PackageManagement.csproj): NuGet Package Management functionality for Visual Studio installation flow.
- [`src\NuGet.Core\NuGet.Packaging.Core\NuGet.Packaging.Core.csproj`](../src/NuGet.Core/NuGet.Packaging.Core/NuGet.Packaging.Core.csproj): The (former home to) core data structures for NuGet.Packaging. Contains only the type forwarders to the new assembly.
- [`src\NuGet.Core\NuGet.Packaging\NuGet.Packaging.csproj`](../src/NuGet.Core/NuGet.Packaging/NuGet.Packaging.csproj): NuGet's understanding of packages. Reading nuspec, nupkgs and package signing.
- [`src\NuGet.Core\NuGet.ProjectModel\NuGet.ProjectModel.csproj`](../src/NuGet.Core/NuGet.ProjectModel/NuGet.ProjectModel.csproj): NuGet's core types and interfaces for PackageReference-based restore, such as lock files, assets file and internal restore models.
- [`src\NuGet.Core\NuGet.Protocol\NuGet.Protocol.csproj`](../src/NuGet.Core/NuGet.Protocol/NuGet.Protocol.csproj): NuGet's implementation for interacting with feeds. Contains functionality for all feed types.
- [`src\NuGet.Core\NuGet.Resolver\NuGet.Resolver.csproj`](../src/NuGet.Core/NuGet.Resolver/NuGet.Resolver.csproj): NuGet's dependency resolver for packages.config based projects.
- [`src\NuGet.Core\NuGet.Versioning\NuGet.Versioning.csproj`](../src/NuGet.Core/NuGet.Versioning/NuGet.Versioning.csproj): NuGet's implementation of Semantic Versioning.


## Test Projects

Most production assemblies has an associated test project, whose name ends with `.Test`.

Test Projects count: 40

- [`test\NuGet.Clients.FuncTests\NuGet.CommandLine.FuncTest\NuGet.CommandLine.FuncTest.csproj`](../test/NuGet.Clients.FuncTests/NuGet.CommandLine.FuncTest/NuGet.CommandLine.FuncTest.csproj): A functional (end-to-end) test suite for NuGet.CommandLine. Contains tests for every nuget.exe command.
- [`test\NuGet.Clients.FuncTests\NuGet.MSSigning.Extensions.FuncTest\NuGet.MSSigning.Extensions.FuncTest.csproj`](../test/NuGet.Clients.FuncTests/NuGet.MSSigning.Extensions.FuncTest/NuGet.MSSigning.Extensions.FuncTest.csproj): A functional (end-to-end) test suite for NuGet.MSSigning.Extensions.
- [`test\NuGet.Clients.Tests\NuGet.CommandLine.Test\NuGet.CommandLine.Test.csproj`](../test/NuGet.Clients.Tests/NuGet.CommandLine.Test/NuGet.CommandLine.Test.csproj): An end-to-end test suite for NuGet.CommandLine. Contains tests for every nuget.exe CLI command. Overlaps in tests with NuGet.CommandLine.FuncTest.
- [`test\NuGet.Clients.Tests\NuGet.MSSigning.Extensions.Test\NuGet.MSSigning.Extensions.Test.csproj`](../test/NuGet.Clients.Tests/NuGet.MSSigning.Extensions.Test/NuGet.MSSigning.Extensions.Test.csproj): An end-to-end test suite for NuGet.MSSigning.Extensions. Overlaps in tests with NuGet.MSSigning.Extensions.FuncTest.
- [`test\NuGet.Clients.Tests\NuGet.PackageManagement.UI.Test\NuGet.PackageManagement.UI.Test.csproj`](../test/NuGet.Clients.Tests/NuGet.PackageManagement.UI.Test/NuGet.PackageManagement.UI.Test.csproj): Unit and integration tests for NuGet.PackageManagement.UI.
- [`test\NuGet.Clients.Tests\NuGet.PackageManagement.VisualStudio.Test\NuGet.PackageManagement.VisualStudio.Test.csproj`](../test/NuGet.Clients.Tests/NuGet.PackageManagement.VisualStudio.Test/NuGet.PackageManagement.VisualStudio.Test.csproj): Unit and integration tests for NuGet.PackageManagement.VisualStudio.
- [`test\NuGet.Clients.Tests\NuGet.SolutionRestoreManager.Test\NuGet.SolutionRestoreManager.Test.csproj`](../test/NuGet.Clients.Tests/NuGet.SolutionRestoreManager.Test/NuGet.SolutionRestoreManager.Test.csproj): Unit and integration tests for NuGet.SolutionRestoreManager.
- [`test\NuGet.Clients.Tests\NuGet.Tools.Test\NuGet.Tools.Test.csproj`](../test/NuGet.Clients.Tests/NuGet.Tools.Test/NuGet.Tools.Test.csproj): Unit and integration tests for NuGet.Tools.
- [`test\NuGet.Clients.Tests\NuGet.VisualStudio.Common.Test\NuGet.VisualStudio.Common.Test.csproj`](../test/NuGet.Clients.Tests/NuGet.VisualStudio.Common.Test/NuGet.VisualStudio.Common.Test.csproj): Unit and integration tests for NuGet.VisualStudio.Common.
- [`test\NuGet.Clients.Tests\NuGet.VisualStudio.Implementation.Test\NuGet.VisualStudio.Implementation.Test.csproj`](../test/NuGet.Clients.Tests/NuGet.VisualStudio.Implementation.Test/NuGet.VisualStudio.Implementation.Test.csproj): Unit and integration tests for NuGet.VisualStudio.Implementation.
- [`test\NuGet.Clients.Tests\NuGet.VisualStudio.Test\NuGet.VisualStudio.Test.csproj`](../test/NuGet.Clients.Tests/NuGet.VisualStudio.Test/NuGet.VisualStudio.Test.csproj): Unit and integration tests for NuGet.VisualStudio.
- [`test\NuGet.Clients.Tests\NuGetConsole.Host.PowerShell.Test\NuGetConsole.Host.PowerShell.Test.csproj`](../test/NuGet.Clients.Tests/NuGetConsole.Host.PowerShell.Test/NuGetConsole.Host.PowerShell.Test.csproj): Unit and integration tests for NuGetConsole.Host.PowerShell.
- [`test\NuGet.Core.FuncTests\Dotnet.Integration.Test\Dotnet.Integration.Test.csproj`](../test/NuGet.Core.FuncTests/Dotnet.Integration.Test/Dotnet.Integration.Test.csproj): Integration tests for NuGet-powered dotnet CLI commands such as pack/restore/list package and dotnet nuget.
- [`test\NuGet.Core.FuncTests\Msbuild.Integration.Test\Msbuild.Integration.Test.csproj`](../test/NuGet.Core.FuncTests/Msbuild.Integration.Test/Msbuild.Integration.Test.csproj): Integration tests for NuGet powered msbuild functionalities (restore/pack).
- [`test\NuGet.Core.FuncTests\NuGet.Commands.FuncTest\NuGet.Commands.FuncTest.csproj`](../test/NuGet.Core.FuncTests/NuGet.Commands.FuncTest/NuGet.Commands.FuncTest.csproj): Integration tests for the more involved NuGet.Commands, such as restore.
- [`test\NuGet.Core.FuncTests\NuGet.Common.FuncTest\NuGet.Common.FuncTest.csproj`](../test/NuGet.Core.FuncTests/NuGet.Common.FuncTest/NuGet.Common.FuncTest.csproj): Functional tests related to networking
- [`test\NuGet.Core.FuncTests\NuGet.Core.FuncTest\NuGet.Core.FuncTest.csproj`](../test/NuGet.Core.FuncTests/NuGet.Core.FuncTest/NuGet.Core.FuncTest.csproj): Integration tests for various functionality from the src/NuGet.Core projects.
- [`test\NuGet.Core.FuncTests\NuGet.Packaging.FuncTest\NuGet.Packaging.FuncTest.csproj`](../test/NuGet.Core.FuncTests/NuGet.Packaging.FuncTest/NuGet.Packaging.FuncTest.csproj): Integration tests for the more involved NuGet.Packaging functionality, such as signing.
- [`test\NuGet.Core.FuncTests\NuGet.Protocol.FuncTest\NuGet.Protocol.FuncTest.csproj`](../test/NuGet.Core.FuncTests/NuGet.Protocol.FuncTest/NuGet.Protocol.FuncTest.csproj): Integration tests for the more involved NuGet.Protocol functionality, such as plugins.
- [`test\NuGet.Core.FuncTests\NuGet.XPlat.FuncTest\NuGet.XPlat.FuncTest.csproj`](../test/NuGet.Core.FuncTests/NuGet.XPlat.FuncTest/NuGet.XPlat.FuncTest.csproj): Functional tests for nuget in dotnet CLI scenarios, using the NuGet.CommandLine.XPlat assembly.
- [`test\NuGet.Core.Tests\Microsoft.Build.NuGetSdkResolver.Tests\Microsoft.Build.NuGetSdkResolver.Test.csproj`](../test/NuGet.Core.Tests/Microsoft.Build.NuGetSdkResolver.Tests/Microsoft.Build.NuGetSdkResolver.Test.csproj): Unit tests for Microsoft.Build.NuGetSdkResolver.
- [`test\NuGet.Core.Tests\NuGet.Build.Tasks.Console.Test\NuGet.Build.Tasks.Console.Test.csproj`](../test/NuGet.Core.Tests/NuGet.Build.Tasks.Console.Test/NuGet.Build.Tasks.Console.Test.csproj): Unit tests for NuGet.Build.Tasks.Console.
- [`test\NuGet.Core.Tests\NuGet.Build.Tasks.Pack.Test\NuGet.Build.Tasks.Pack.Test.csproj`](../test/NuGet.Core.Tests/NuGet.Build.Tasks.Pack.Test/NuGet.Build.Tasks.Pack.Test.csproj): Unit tests for NuGet.Build.Tasks.Pack.
- [`test\NuGet.Core.Tests\NuGet.Build.Tasks.Test\NuGet.Build.Tasks.Test.csproj`](../test/NuGet.Core.Tests/NuGet.Build.Tasks.Test/NuGet.Build.Tasks.Test.csproj): Unit tests for NuGet.Build.Tasks.
- [`test\NuGet.Core.Tests\NuGet.CommandLine.Xplat.Tests\NuGet.CommandLine.Xplat.Tests.csproj`](../test/NuGet.Core.Tests/NuGet.CommandLine.Xplat.Tests/NuGet.CommandLine.Xplat.Tests.csproj): Unit tests for NuGet.CommandLine.XPlat.
- [`test\NuGet.Core.Tests\NuGet.Commands.Test\NuGet.Commands.Test.csproj`](../test/NuGet.Core.Tests/NuGet.Commands.Test/NuGet.Commands.Test.csproj): Unit tests for NuGet.Commands.
- [`test\NuGet.Core.Tests\NuGet.Common.Test\NuGet.Common.Test.csproj`](../test/NuGet.Core.Tests/NuGet.Common.Test/NuGet.Common.Test.csproj): Unit tests for NuGet.Common.
- [`test\NuGet.Core.Tests\NuGet.Configuration.Test\NuGet.Configuration.Test.csproj`](../test/NuGet.Core.Tests/NuGet.Configuration.Test/NuGet.Configuration.Test.csproj): Unit tests for NuGet.Configuration.
- [`test\NuGet.Core.Tests\NuGet.Credentials.Test\NuGet.Credentials.Test.csproj`](../test/NuGet.Core.Tests/NuGet.Credentials.Test/NuGet.Credentials.Test.csproj): Unit tests for NuGet.Credentials.
- [`test\NuGet.Core.Tests\NuGet.DependencyResolver.Core.Tests\NuGet.DependencyResolver.Core.Tests.csproj`](../test/NuGet.Core.Tests/NuGet.DependencyResolver.Core.Tests/NuGet.DependencyResolver.Core.Tests.csproj): Unit tests for NuGet.DependencyResolver.Core.
- [`test\NuGet.Core.Tests\NuGet.Frameworks.Test\NuGet.Frameworks.Test.csproj`](../test/NuGet.Core.Tests/NuGet.Frameworks.Test/NuGet.Frameworks.Test.csproj): Unit tests for NuGet.Frameworks.
- [`test\NuGet.Core.Tests\NuGet.Indexing.Test\NuGet.Indexing.Test.csproj`](../test/NuGet.Core.Tests/NuGet.Indexing.Test/NuGet.Indexing.Test.csproj): Unit tests for NuGet.Indexing.
- [`test\NuGet.Core.Tests\NuGet.LibraryModel.Tests\NuGet.LibraryModel.Tests.csproj`](../test/NuGet.Core.Tests/NuGet.LibraryModel.Tests/NuGet.LibraryModel.Tests.csproj): Unit tests for NuGet.LibraryModel.
- [`test\NuGet.Core.Tests\NuGet.PackageManagement.Test\NuGet.PackageManagement.Test.csproj`](../test/NuGet.Core.Tests/NuGet.PackageManagement.Test/NuGet.PackageManagement.Test.csproj): Unit tests for NuGet.PackageManagement.
- [`test\NuGet.Core.Tests\NuGet.Packaging.Test\NuGet.Packaging.Test.csproj`](../test/NuGet.Core.Tests/NuGet.Packaging.Test/NuGet.Packaging.Test.csproj): Unit tests for NuGet.Packaging.
- [`test\NuGet.Core.Tests\NuGet.ProjectModel.Test\NuGet.ProjectModel.Test.csproj`](../test/NuGet.Core.Tests/NuGet.ProjectModel.Test/NuGet.ProjectModel.Test.csproj): Unit tests for NuGet.ProjectModel.
- [`test\NuGet.Core.Tests\NuGet.Protocol.Tests\NuGet.Protocol.Tests.csproj`](../test/NuGet.Core.Tests/NuGet.Protocol.Tests/NuGet.Protocol.Tests.csproj): Unit tests for NuGet.Protocol.
- [`test\NuGet.Core.Tests\NuGet.Resolver.Test\NuGet.Resolver.Test.csproj`](../test/NuGet.Core.Tests/NuGet.Resolver.Test/NuGet.Resolver.Test.csproj): Unit tests for NuGet.Resolver.
- [`test\NuGet.Core.Tests\NuGet.Shared.Tests\NuGet.Shared.Tests.csproj`](../test/NuGet.Core.Tests/NuGet.Shared.Tests/NuGet.Shared.Tests.csproj): Unit tests for the utilities included using shared compilation.
- [`test\NuGet.Core.Tests\NuGet.Versioning.Test\NuGet.Versioning.Test.csproj`](../test/NuGet.Core.Tests/NuGet.Versioning.Test/NuGet.Versioning.Test.csproj): Unit tests for NuGet.Versioning.
