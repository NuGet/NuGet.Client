// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Build", "CA1062:In externally visible method 'void AddPackageReferenceCommand.Register(CommandLineApplication app, Func<ILogger> getLogger, Func<IPackageReferenceCommandRunner> getCommandRunner)', validate parameter 'app' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.CommandLine.XPlat.AddPackageReferenceCommand.Register(Microsoft.Extensions.CommandLineUtils.CommandLineApplication,System.Func{NuGet.Common.ILogger},System.Func{NuGet.CommandLine.XPlat.IPackageReferenceCommandRunner})")]
[assembly: SuppressMessage("Build", "CA1062:In externally visible method 'Task<int> AddPackageReferenceCommandRunner.ExecuteCommand(PackageReferenceArgs packageReferenceArgs, MSBuildAPIUtility msBuild)', validate parameter 'msBuild' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.CommandLine.XPlat.AddPackageReferenceCommandRunner.ExecuteCommand(NuGet.CommandLine.XPlat.PackageReferenceArgs,NuGet.CommandLine.XPlat.MSBuildAPIUtility)~System.Threading.Tasks.Task{System.Int32}")]
[assembly: SuppressMessage("Build", "CA1308:In method 'LogInternal', replace the call to 'ToLowerInvariant' with 'ToUpperInvariant'.", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.CommandLine.XPlat.CommandOutputLogger.LogInternal(NuGet.Common.LogLevel,System.String)")]
[assembly: SuppressMessage("Build", "CA1062:In externally visible method 'void CommandOutputLogger.LogInternal(LogLevel logLevel, string message)', validate parameter 'message' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.CommandLine.XPlat.CommandOutputLogger.LogInternal(NuGet.Common.LogLevel,System.String)")]
[assembly: SuppressMessage("Build", "CA1822:Member GetUpdateLevel does not access instance data and can be marked as static (Shared in VisualBasic)", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.CommandLine.XPlat.ListPackageCommandRunner.GetUpdateLevel(NuGet.Versioning.NuGetVersion,NuGet.Versioning.NuGetVersion)~NuGet.CommandLine.XPlat.UpdateLevel")]
[assembly: SuppressMessage("Build", "CA1822:Member MeetsConstraints does not access instance data and can be marked as static (Shared in VisualBasic)", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.CommandLine.XPlat.ListPackageCommandRunner.MeetsConstraints(NuGet.Versioning.NuGetVersion,NuGet.CommandLine.XPlat.InstalledPackageReference,NuGet.CommandLine.XPlat.ListPackageArgs)~System.Boolean")]
[assembly: SuppressMessage("Build", "CA1062:In externally visible method 'void MSBuildAPIUtility.AddPackageReferencePerTFM(string projectPath, LibraryDependency libraryDependency, IEnumerable<string> frameworks, bool noVersion)', validate parameter 'libraryDependency' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.CommandLine.XPlat.MSBuildAPIUtility.AddPackageReferencePerTFM(System.String,NuGet.LibraryModel.LibraryDependency,System.Collections.Generic.IEnumerable{System.String},System.Boolean)")]
[assembly: SuppressMessage("Build", "CA1062:In externally visible method 'int MSBuildAPIUtility.RemovePackageReference(string projectPath, LibraryDependency libraryDependency)', validate parameter 'libraryDependency' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.CommandLine.XPlat.MSBuildAPIUtility.RemovePackageReference(System.String,NuGet.LibraryModel.LibraryDependency)~System.Int32")]
[assembly: SuppressMessage("Build", "CA1062:In externally visible method 'int Program.MainInternal(string[] args, CommandOutputLogger log)', validate parameter 'log' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.CommandLine.XPlat.Program.MainInternal(System.String[],NuGet.CommandLine.XPlat.CommandOutputLogger)~System.Int32")]
[assembly: SuppressMessage("Build", "CA1308:In method 'MainInternal', replace the call to 'ToLowerInvariant' with 'ToUpperInvariant'.", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.CommandLine.XPlat.Program.MainInternal(System.String[],NuGet.CommandLine.XPlat.CommandOutputLogger)~System.Int32")]
[assembly: SuppressMessage("Build", "CA1031:Modify 'MainInternal' to catch a more specific allowed exception type, or rethrow the exception.", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.CommandLine.XPlat.Program.MainInternal(System.String[],NuGet.CommandLine.XPlat.CommandOutputLogger)~System.Int32")]
[assembly: SuppressMessage("Build", "CA1303:Method 'int Program.MainInternal(string[] args, CommandOutputLogger log)' passes a literal string as parameter 'value' of a call to 'void Console.WriteLine(string value)'. Retrieve the following string(s) from a resource table instead: \"Waiting for debugger to attach.\".", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.CommandLine.XPlat.Program.MainInternal(System.String[],NuGet.CommandLine.XPlat.CommandOutputLogger)~System.Int32")]
[assembly: SuppressMessage("Build", "CA1062:In externally visible method 'void RemovePackageReferenceCommand.Register(CommandLineApplication app, Func<ILogger> getLogger, Func<IPackageReferenceCommandRunner> getCommandRunner)', validate parameter 'app' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.CommandLine.XPlat.RemovePackageReferenceCommand.Register(Microsoft.Extensions.CommandLineUtils.CommandLineApplication,System.Func{NuGet.Common.ILogger},System.Func{NuGet.CommandLine.XPlat.IPackageReferenceCommandRunner})")]
[assembly: SuppressMessage("Build", "CA1062:In externally visible method 'Task<int> RemovePackageReferenceCommandRunner.ExecuteCommand(PackageReferenceArgs packageReferenceArgs, MSBuildAPIUtility msBuild)', validate parameter 'msBuild' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.CommandLine.XPlat.RemovePackageReferenceCommandRunner.ExecuteCommand(NuGet.CommandLine.XPlat.PackageReferenceArgs,NuGet.CommandLine.XPlat.MSBuildAPIUtility)~System.Threading.Tasks.Task{System.Int32}")]
[assembly: SuppressMessage("Build", "CA1819:Properties should not return arrays", Justification = "<Pending>", Scope = "member", Target = "~P:NuGet.CommandLine.XPlat.PackageReferenceArgs.Frameworks")]
[assembly: SuppressMessage("Build", "CA1819:Properties should not return arrays", Justification = "<Pending>", Scope = "member", Target = "~P:NuGet.CommandLine.XPlat.PackageReferenceArgs.Sources")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.CommandLine.XPlat.ConfigCommand.RegisterOptionsForCommandConfigPaths(System.CommandLine.CliCommand,System.Func{NuGet.Common.ILogger})")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.CommandLine.XPlat.Table.#ctor(System.Int32[],System.String[])")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.CommandLine.XPlat.ConfigCommand.RegisterOptionsForCommandConfigGet(System.CommandLine.CliCommand,System.Func{NuGet.Common.ILogger})")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.CommandLine.XPlat.ConfigCommand.RegisterOptionsForCommandConfigSet(System.CommandLine.CliCommand,System.Func{NuGet.Common.ILogger})")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.CommandLine.XPlat.ConfigCommand.RegisterOptionsForCommandConfigUnset(System.CommandLine.CliCommand,System.Func{NuGet.Common.ILogger})")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.CommandLine.XPlat.ListPackageCommandRunner.ThrottledForEachAsync``2(System.Collections.Generic.IList{``0},System.Func{``0,System.Threading.CancellationToken,System.Threading.Tasks.Task{``1}},System.Action{``1},System.Int32,System.Threading.CancellationToken)~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.CommandLine.XPlat.MSBuildAPIUtility.AddExtraMetadataToProjectItemElement(NuGet.LibraryModel.LibraryDependency,Microsoft.Build.Construction.ProjectItemElement)")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.CommandLine.XPlat.MSBuildAPIUtility.AddVersionMetadata(NuGet.LibraryModel.LibraryDependency,Microsoft.Build.Construction.ProjectItemElement)~System.String")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.CommandLine.XPlat.MSBuildAPIUtility.AreCentralVersionRequirementsSatisfied(NuGet.CommandLine.XPlat.PackageReferenceArgs,NuGet.ProjectModel.PackageSpec)~System.Boolean")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.CommandLine.XPlat.MSBuildAPIUtility.CreateItemGroup(Microsoft.Build.Evaluation.Project,System.String)~Microsoft.Build.Construction.ProjectItemGroupElement")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.CommandLine.XPlat.MSBuildAPIUtility.GetDirectoryPackagePropsRootElement(Microsoft.Build.Evaluation.Project)~Microsoft.Build.Construction.ProjectRootElement")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.CommandLine.XPlat.MSBuildAPIUtility.GetItemGroup(System.Collections.Generic.IEnumerable{Microsoft.Build.Construction.ProjectItemGroupElement},System.String)~Microsoft.Build.Construction.ProjectItemGroupElement")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.CommandLine.XPlat.MSBuildAPIUtility.GetItemGroups(Microsoft.Build.Evaluation.Project)~System.Collections.Generic.IEnumerable{Microsoft.Build.Construction.ProjectItemGroupElement}")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.CommandLine.XPlat.MSBuildAPIUtility.UpdateExtraMetadata(NuGet.LibraryModel.LibraryDependency,Microsoft.Build.Evaluation.ProjectItem)")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.CommandLine.XPlat.MSBuildAPIUtility.UpdateExtraMetadataInProjectItem(NuGet.LibraryModel.LibraryDependency,Microsoft.Build.Evaluation.ProjectItem)")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.CommandLine.XPlat.MSBuildAPIUtility.UpdatePackageVersion(Microsoft.Build.Evaluation.Project,Microsoft.Build.Evaluation.ProjectItem,System.String)")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.CommandLine.XPlat.MSBuildAPIUtility.UpdateVersionOverride(Microsoft.Build.Evaluation.Project,Microsoft.Build.Evaluation.ProjectItem,System.String)")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.CommandLine.XPlat.PackageReferenceArgs.ValidateArgument(System.Object)")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.CommandLine.XPlat.PackageSearchArgs.GetFormatFromOption(System.String)~NuGet.CommandLine.XPlat.PackageSearchFormat")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.CommandLine.XPlat.PackageSearchArgs.GetVerbosityFromOption(System.String)~NuGet.CommandLine.XPlat.PackageSearchVerbosity")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.CommandLine.XPlat.PackageSearchArgs.VerifyInt(System.String,System.Int32,System.String)~System.Int32")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.CommandLine.XPlat.Table.SanitizeString(System.String)~System.String")]
[assembly: SuppressMessage("Globalization", "CA1305:Specify IFormatProvider", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.CommandLine.XPlat.ListPackage.ListPackageConsoleRenderer.GetProjectHeader(System.String,NuGet.CommandLine.XPlat.ListPackageArgs)~System.String")]
[assembly: SuppressMessage("Globalization", "CA1305:Specify IFormatProvider", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.CommandLine.XPlat.ListPackageCommand.GetOutputType(System.String,System.String)~NuGet.CommandLine.XPlat.ListPackage.IReportRenderer")]
[assembly: SuppressMessage("Globalization", "CA1305:Specify IFormatProvider", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.CommandLine.XPlat.UILanguageOverride.SetIfNotAlreadySet(System.String,System.Int32)")]
