// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.Core.FuncTest.DownloadTimeoutStreamTests.GetStream(System.String)~System.IO.MemoryStream")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.Core.FuncTest.DownloadTimeoutStreamTests.ReadStream(System.IO.Stream)~System.String")]
[assembly: SuppressMessage("Reliability", "CA2016:Forward the 'CancellationToken' parameter to methods", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.Core.FuncTest.HttpRetryHandlerTests.HttpRetryHandler_AppliesTimeoutToRequestsIndividually~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.Core.FuncTest.ResolverTests.CreatePackage(System.String,System.String,System.Collections.Generic.IDictionary{System.String,System.String})~NuGet.Resolver.ResolverPackage")]
[assembly: SuppressMessage("Performance", "CA1827:Do not use Count() or LongCount() when Any() can be used", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.Protocol.FuncTest.DownloadResourceV2FeedTests.PackageMetadataVersionsFromIdentity~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("Performance", "CA1826:Do not use Enumerable methods on indexable collections", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.Protocol.FuncTest.V2FeedParserTests.V2FeedParser_Search(System.String)~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("Performance", "CA1826:Do not use Enumerable methods on indexable collections", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.Protocol.FuncTest.V2FeedParserTests.V2FeedParser_SearchFromCredentialServer(NuGet.Configuration.PackageSource)~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("Performance", "CA1826:Do not use Enumerable methods on indexable collections", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.Protocol.FuncTest.V2FeedParserTests.V2FeedParser_SearchWithPortableFramework(System.String)~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("Performance", "CA1826:Do not use Enumerable methods on indexable collections", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.Protocol.FuncTest.V2FeedParserTests.V2FeedParser_SearchWithPortableFrameworkFromCredentialServer(NuGet.Configuration.PackageSource)~System.Threading.Tasks.Task")]
