// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

[assembly: AssemblyTitle("NuGet.Tools")]
[assembly: AssemblyDescription("Visual Studio Extensibility Package (vsix)")]
[assembly: ComVisible(false)]

[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Lucene.Net.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Microsoft.Web.XmlTransform.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Newtonsoft.Json.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\NuGet.Commands.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\NuGet.Common.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\NuGet.Configuration.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\NuGet.Console.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\NuGet.Credentials.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\NuGet.DependencyResolver.Core.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\NuGet.Frameworks.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\NuGet.Indexing.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\NuGet.LibraryModel.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\NuGet.PackageManagement.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\NuGet.PackageManagement.PowerShellCmdlets.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\NuGet.PackageManagement.UI.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\NuGet.PackageManagement.VisualStudio.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\NuGet.Packaging.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\NuGet.ProjectModel.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\NuGet.Protocol.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\NuGet.Resolver.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\NuGet.Tools.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\NuGet.Versioning.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\NuGet.VisualStudio.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\NuGet.VisualStudio.Common.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\NuGet.VisualStudio.Implementation.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\NuGet.VisualStudio.Interop.dll")]

[assembly: ProvideBindingRedirection(CodeBase = @"$PackageFolder$\NuGet.VisualStudio.Contracts.dll", OldVersionLowerBound = "0.0.0.0")]

#if SIGNED_BUILD
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2, PublicKey=0024000004800000940000000602000000240000525341310004000001000100c547cac37abd99c8db225ef2f6c8a3602f3b3606cc9891605d02baa56104f4cfc0734aa39b93bf7852f7d9266654753cc297e7d2edfe0bac1cdcf9f717241550e0a7b191195b7667bb4f64bcb8e2121380fd1d9d46ad2d92d2d15605093924cceaf74c4861eff62abf69b9291ed0a340e113be11e6a7d3113e92484cf7045cc7")]
#else
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
#endif
