// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Performance", "CA1825:Avoid zero-length array allocations", Justification = "<Pending>", Scope = "member", Target = "~F:NuGet.XPlat.FuncTest.AddPackageCommandUtilityTests.GetLatesVersionFromSourcesData")]
[assembly: SuppressMessage("Performance", "CA1829:Use Length/Count property instead of Count() when available", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.XPlat.FuncTest.AddPackageCommandUtilityTests.GetSourceWithPackages(System.String[],NuGet.Test.Utility.TestDirectory,System.String)~System.Threading.Tasks.Task{System.String}")]
[assembly: SuppressMessage("Performance", "CA1825:Avoid zero-length array allocations", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.XPlat.FuncTest.BasicLoggingTests.BasicLogging_NoParams_ExitCode")]
[assembly: SuppressMessage("Performance", "CA1829:Use Length/Count property instead of Count() when available", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.XPlat.FuncTest.DotnetCliUtil.VerifyNoClear(System.String)")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.XPlat.FuncTest.ListPackageTests.VerifyCommand(System.Action{System.String,Moq.Mock{NuGet.CommandLine.XPlat.IListPackageCommandRunner},Microsoft.Extensions.CommandLineUtils.CommandLineApplication,System.Func{NuGet.Common.LogLevel}})")]
[assembly: SuppressMessage("Performance", "CA1829:Use Length/Count property instead of Count() when available", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.XPlat.FuncTest.XPlatAddPkgTests.AddPkg_V3LocalSourceFeed_WithAbsolutePath_NoVersionSpecified_Success~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("Performance", "CA1829:Use Length/Count property instead of Count() when available", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.XPlat.FuncTest.XPlatAddPkgTests.AddPkg_V3LocalSourceFeed_WithAbsolutePath_VersionSpecified_Success~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("Usage", "CA1816:Dispose methods should call SuppressFinalize", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.XPlat.FuncTest.XPlatMsbuildTestFixture.Dispose")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.XPlat.FuncTest.XplatSignTests.SignCommandArgs(System.Action{Moq.Mock{NuGet.Commands.ISignCommandRunner},Microsoft.Extensions.CommandLineUtils.CommandLineApplication,System.Func{NuGet.Common.LogLevel},System.Func{NuGet.Commands.SignArgs}})")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.XPlat.FuncTest.XPlatTrustTests.TrustCommandArgs(System.Action{Microsoft.Extensions.CommandLineUtils.CommandLineApplication,System.Func{NuGet.Common.LogLevel}})")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.XPlat.FuncTest.XPlatVerifyTests.VerifyCommandArgs(System.Action{Moq.Mock{NuGet.Commands.IVerifyCommandRunner},Microsoft.Extensions.CommandLineUtils.CommandLineApplication,System.Func{NuGet.Common.LogLevel}})")]
