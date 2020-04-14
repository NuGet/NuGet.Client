﻿// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Build", "CA1307:The behavior of 'string.Equals(string)' could vary based on the current user's locale settings. Replace this call in 'NuGet.VisualStudio.OnlineEnvironment.Client.NuGetWorkspaceCommandHandler.IsSolutionOnlySelection(System.Collections.Generic.List<Microsoft.VisualStudio.Workspace.VSIntegration.UI.WorkspaceVisualNodeBase>)' with a call to 'string.Equals(string, System.StringComparison)'.", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.VisualStudio.OnlineEnvironment.Client.NuGetWorkspaceCommandHandler.IsSolutionOnlySelection(System.Collections.Generic.List{Microsoft.VisualStudio.Workspace.VSIntegration.UI.WorkspaceVisualNodeBase})~System.Boolean")]
[assembly: SuppressMessage("Build", "CA1031:Modify 'RunSolutionRestoreAsync' to catch a more specific allowed exception type, or rethrow the exception.", Justification = "<Pending>", Scope = "member", Target = "~M:NuGet.VisualStudio.OnlineEnvironment.Client.RestoreCommandHandler.RunSolutionRestoreAsync~System.Threading.Tasks.Task")]
