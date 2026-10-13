// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using NuGet.PackageManagement.VisualStudio;
using NuGet.VisualStudio.Common.Telemetry.PowerShell;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    [Cmdlet(VerbsData.Import, "PackageInitScript", DefaultParameterSetName = "All")]
    public sealed class ImportPackageInitScriptCommand : NuGetPowerShellBaseCommand
    {
        private readonly HashSet<string> _selectedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private IReadOnlyList<PowerShellPackageInitScript>? _scripts;

        [Parameter(ParameterSetName = "Packages")]
        [ValidateNotNullOrEmpty]
        public string[]? Packages { get; set; }

        protected override bool IsLoggingTimeDisabled => true;

        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, ParameterSetName = "Paths")]
        [Alias("Path")]
        [System.Management.Automation.AllowNull]
        [AllowEmptyCollection]
        public object[]? FullName { get; set; }

        protected override void ProcessRecordCore()
        {
            if (ParameterSetName != "Paths")
            {
                foreach (var script in PackageInitScriptHelper.GetMatchingScripts(GetScripts(), Packages, WriteError))
                {
                    _selectedPaths.Add(script.FullName);
                }
                return;
            }

            if (FullName == null)
            {
                return;
            }

            foreach (var input in FullName)
            {
                if (input == null)
                {
                    continue;
                }

                var wrapped = PSObject.AsPSObject(input);
                var path = wrapped.BaseObject as string ?? wrapped.Properties["FullName"]?.Value as string;
                string? fullPath = null;
                if (!string.IsNullOrWhiteSpace(path))
                {
                    var resolvedPath = SessionState.Path.GetUnresolvedProviderPathFromPSPath(path, out var provider, out _);
                    if (StringComparer.OrdinalIgnoreCase.Equals(provider.Name, "FileSystem"))
                    {
                        fullPath = resolvedPath;
                    }
                }
                var script = GetScripts().FirstOrDefault(candidate =>
                    StringComparer.OrdinalIgnoreCase.Equals(candidate.FullName, fullPath));

                if (script == null)
                {
                    WriteError(new ErrorRecord(
                        new ArgumentException(string.Format(CultureInfo.CurrentCulture, Resources.InitScript_InvalidPath, path ?? input.ToString())),
                        NuGetErrorId.InvalidPackageInitScript, ErrorCategory.InvalidArgument, input));
                }
                else
                {
                    _selectedPaths.Add(script.FullName);
                }
            }
        }

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Report script failures through the cmdlet error handler.")]
        protected override void EndProcessing()
        {
            try
            {
                if (_selectedPaths.Count > 0)
                {
                    // Collect pipeline input first so scripts still run in dependency order.
                    foreach (var script in GetScripts().Where(script => _selectedPaths.Contains(script.FullName)))
                    {
                        Token.ThrowIfCancellationRequested();
                        var request = new ScriptExecutionRequest(script.FullName, script.InstallPath, script.Identity, project: null);
                        AddToolsPath(request.ToolsPath);
                        NuGetPowerShellUsage.RaiseInitPs1LoadEvent(isPMC: true);
                        WriteVerbose(string.Format(CultureInfo.CurrentCulture, Resources.Cmdlet_ExecutingScript, script.FullName));
                        try
                        {
                            InvokeCommand.InvokeScript(request.BuildCommand(), false,
                                PipelineResultTypes.Output | PipelineResultTypes.Error,
                                new ArrayList(request.BuildInput()));
                        }
                        finally
                        {
                            SessionState.PSVariable.Remove("__pc_args");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionHelper.WriteErrorToActivityLog(ex);
                ErrorHandler.HandleException(ex, terminating: true);
            }
            finally
            {
                base.EndProcessing();
            }
        }

        private IReadOnlyList<PowerShellPackageInitScript> GetScripts()
        {
            if (_scripts == null)
            {
                CheckSolutionState();
                _scripts = PackageInitScriptHelper.GetScripts(VsSolutionManager, ConfigSettings, PackageManager, Token);
            }

            return _scripts;
        }

        private void AddToolsPath(string toolsPath)
        {
            var currentPath = SessionState.PSVariable.GetValue("env:PATH") as string ?? string.Empty;
            if (!currentPath.Split(Path.PathSeparator).Any(path =>
                StringComparer.OrdinalIgnoreCase.Equals(path.Trim(), toolsPath)))
            {
                SessionState.PSVariable.Set("env:PATH", currentPath + Path.PathSeparator + toolsPath);
            }
        }
    }
}
