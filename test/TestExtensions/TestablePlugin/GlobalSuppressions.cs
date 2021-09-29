// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.Test.TestExtensions.TestablePlugin.Program.Main(System.String[])~System.Int32")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.Test.TestExtensions.TestablePlugin.Program.Start(NuGet.Test.TestExtensions.TestablePlugin.Arguments)")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.Test.TestExtensions.TestablePlugin.Program.TryGetProcess(System.Int32,System.Diagnostics.Process@)~System.Boolean")]
[assembly: SuppressMessage("Reliability", "CA2016:Forward the 'CancellationToken' parameter to methods", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.Test.TestExtensions.TestablePlugin.ResponseReceiver.StartListeningAsync(System.Threading.CancellationToken)~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("Reliability", "CA2016:Forward the 'CancellationToken' parameter to methods", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.Test.TestExtensions.TestablePlugin.TestablePlugin.HandleResponseAsync(NuGet.Protocol.Plugins.IConnection,NuGet.Protocol.Plugins.Message,NuGet.Protocol.Plugins.IResponseHandler,System.Threading.CancellationToken)~System.Threading.Tasks.Task")]
