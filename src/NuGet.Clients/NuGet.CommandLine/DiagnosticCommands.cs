// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Frameworks;
using NuGet.ProjectModel;

namespace NuGet.CommandLine
{
    public class DiagnosticCommands
    {
        private readonly Logging.ILogger _log;

        private const string Native = "\x1b[31mnative\x1b[39m";
        private const string Runtime = "\x1b[35mruntime\x1b[39m";
        private const string Compile = "\x1b[32mcompile\x1b[39m";
        private const string Framework = "\x1b[34mframework\x1b[39m";
        private const string Nothing = "\x1b[33mnothing\x1b[39m";

        public DiagnosticCommands(Logging.ILogger log)
        {
            _log = log;
        }

        public int Lockfile(string projectOrLockfile, string target, string library)
        {
            // Locate the lock file
            if (!string.Equals(Path.GetFileName(projectOrLockfile), LockFileFormat.LockFileName, StringComparison.Ordinal))
            {
                projectOrLockfile = Path.Combine(projectOrLockfile, LockFileFormat.LockFileName);
            }
            var lockfile = new LockFileFormat().Read(projectOrLockfile);
            _log.LogInformation($"Viewing data from Lock File: {projectOrLockfile}");

            // Attempt to locate the project
            var projectPath = Path.Combine(Path.GetDirectoryName(projectOrLockfile), PackageSpec.PackageSpecFileName);
            PackageSpec project = null;
            if (File.Exists(projectPath))
            {
                project = JsonPackageSpecReader.GetPackageSpec(File.ReadAllText(projectPath), Path.GetFileName(Path.GetDirectoryName(projectPath)), projectPath);
            }

            if (string.IsNullOrEmpty(library))
            {
                return SummarizeLockfile(project, lockfile, target);
            }
            else
            {
                return LibraryDetail(project, lockfile, target, library);
            }
        }

        private int LibraryDetail(PackageSpec project, LockFile lockfile, string targetName, string library)
        {
            var lib = lockfile.Libraries.FirstOrDefault(l => l.Name.Equals(library, StringComparison.OrdinalIgnoreCase));
            if (lib == null)
            {
                _log.LogError($"Library not found: {library}");
                return -1;
            }

            _log.LogInformation($"{lib.Name} {lib.Version}");
            _log.LogInformation($"Servicable: {lib.IsServiceable}");
            _log.LogInformation($"SHA512 Hash: {lib.Sha512}");
            _log.LogInformation($"Files:");
            foreach (var file in lib.Files)
            {
                _log.LogInformation($" * {file}");
            }

            IEnumerable<LockFileTarget> targets = lockfile.Targets;
            if (!string.IsNullOrEmpty(targetName))
            {
                var parts = targetName.Split('/');
                var tfm = NuGetFramework.Parse(parts[0]);
                var rid = parts[1];
                targets = targets.Where(t => string.Equals(rid, t.RuntimeIdentifier, StringComparison.Ordinal) && tfm.Equals(t.TargetFramework));
            }
            var libraryTargets = targets.Select(t => new { Target = t, Library = t.Libraries.FirstOrDefault(l => string.Equals(l.Name, library, StringComparison.OrdinalIgnoreCase)) });
            foreach (var libraryTarget in libraryTargets)
            {
                _log.LogInformation($"Target: {libraryTarget.Target.TargetFramework}/{libraryTarget.Target.RuntimeIdentifier}");
                if (libraryTarget.Library == null)
                {
                    _log.LogInformation(" Not supported");
                }
                else
                {
                    WriteList(Compile, libraryTarget.Library.CompileTimeAssemblies.Select(f => f.Path));
                    WriteList(Runtime, libraryTarget.Library.RuntimeAssemblies.Select(f => f.Path));
                    WriteList(Native, libraryTarget.Library.NativeLibraries.Select(f => f.Path));
                    WriteList(Framework, libraryTarget.Library.FrameworkAssemblies);
                }
            }

            return 0;
        }

        private void WriteList(string header, IEnumerable<string> items)
        {
            _log.LogInformation($" {header}:");
            foreach (var item in items)
            {
                _log.LogInformation($"  * {item}");
            }
        }

        private int SummarizeLockfile(PackageSpec project, LockFile lockfile, string targetName)
        {
            _log.LogInformation($"Locked: {lockfile.IsLocked}");

            if (project == null)
            {
                _log.LogInformation($"Up-to-date: Unknown");
            }
            else
            {
                _log.LogInformation($"Up-to-date: {lockfile.IsValidForPackageSpec(project)}");
            }

            _log.LogInformation("Project Dependencies:");
            foreach (var group in lockfile.ProjectFileDependencyGroups)
            {
                var fxName = string.IsNullOrEmpty(group.FrameworkName) ? "All Frameworks" : group.FrameworkName;
                if (group.Dependencies.Any())
                {
                    _log.LogInformation($" {fxName}");
                    foreach (var dep in group.Dependencies)
                    {
                        _log.LogInformation($"  * {dep}");
                    }
                }
                else
                {
                    _log.LogInformation($" {fxName}: none");
                }
            }

            _log.LogInformation("All Libraries:");
            foreach (var lib in lockfile.Libraries)
            {
                _log.LogInformation($"* {lib.Name} {lib.Version}");
            }

            IEnumerable<LockFileTarget> targets = lockfile.Targets;
            if (!string.IsNullOrEmpty(targetName))
            {
                var parts = targetName.Split('/');
                var tfm = NuGetFramework.Parse(parts[0]);
                var rid = parts[1];
                targets = targets.Where(t => string.Equals(rid, t.RuntimeIdentifier, StringComparison.Ordinal) && tfm.Equals(t.TargetFramework));
            }

            foreach (var target in targets)
            {
                _log.LogInformation($"Target: {target.TargetFramework} {target.RuntimeIdentifier}");
                foreach (var lib in target.Libraries)
                {
                    var provides = string.Empty;
                    if (lib.NativeLibraries.Any())
                    {
                        provides += Native + ",";
                    }
                    if (lib.RuntimeAssemblies.Any())
                    {
                        provides += Runtime + ",";
                    }
                    if (lib.CompileTimeAssemblies.Any())
                    {
                        provides += Compile + ",";
                    }
                    if (lib.FrameworkAssemblies.Any())
                    {
                        provides += Framework + ",";
                    }
                    provides = provides.TrimEnd(',');
                    if (string.IsNullOrEmpty(provides))
                    {
                        provides = Nothing;
                    }
                    _log.LogInformation($" * [{provides}] {lib.Name} {lib.Version}");
                }
            }
            return 0;
        }
    }
}
