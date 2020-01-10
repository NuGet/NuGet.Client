
// This file is used by Code Analysis to maintain SuppressMessage 
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given 
// a specific target and scoped to a namespace, type, member, etc.

[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD110:Observe result of async calls", Justification = "https://github.com/NuGet/Home/issues/7674", Scope = "member", Target = "~M:NuGet.SolutionRestoreManager.RestoreEventPublisher.OnSolutionRestoreCompleted(NuGet.VisualStudio.SolutionRestoredEventArgs)")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "VSSDK004:Use BackgroundLoad flag in ProvideAutoLoad attribute for asynchronous auto load.", Justification = "https://github.com/NuGet/Home/issues/8796", Scope = "type", Target = "~T:NuGet.SolutionRestoreManager.RestoreManagerPackage")]
