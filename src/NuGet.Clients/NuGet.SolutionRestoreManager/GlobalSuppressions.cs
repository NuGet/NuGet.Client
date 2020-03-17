
// This file is used by Code Analysis to maintain SuppressMessage 
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given 
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Usage", "VSTHRD110:Observe result of async calls", Justification = "https://github.com/NuGet/Home/issues/7674", Scope = "member", Target = "~M:NuGet.SolutionRestoreManager.RestoreEventPublisher.OnSolutionRestoreCompleted(NuGet.VisualStudio.SolutionRestoredEventArgs)")]
[assembly: SuppressMessage("Performance", "VSSDK004:Use BackgroundLoad flag in ProvideAutoLoad attribute for asynchronous auto load.", Justification = "https://github.com/NuGet/Home/issues/8796", Scope = "type", Target = "~T:NuGet.SolutionRestoreManager.RestoreManagerPackage")]
[assembly: SuppressMessage("Usage", "VSTHRD010:Invoke single-threaded types on Main thread", Justification = "https://github.com/microsoft/vs-threading/issues/577", Scope = "member", Target = "~M:NuGet.SolutionRestoreManager.SolutionRestoreJob.CheckPackagesConfigAsync~System.Threading.Tasks.Task{System.Boolean}")]
[assembly: SuppressMessage("Usage", "VSTHRD108:Assert thread affinity unconditionally", Justification = "Unclear what the consequences when the dispose is called from the analyzer", Scope = "member", Target = "~M:NuGet.SolutionRestoreManager.RestoreManagerPackage.Dispose(System.Boolean)")]
