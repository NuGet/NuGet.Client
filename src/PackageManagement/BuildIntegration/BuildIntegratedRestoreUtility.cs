using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Framework.Logging;
using NuGet.Commands;
using NuGet.Configuration;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectModel;

namespace NuGet.PackageManagement
{
    /// <summary>
    /// Helper class for calling DNU restore
    /// </summary>
    public static class BuildIntegratedRestoreUtility
    {
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public static async Task RestoreForBuild(string jsonConfigPath,
            string projectName,
            INuGetProjectContext projectContext,
            IEnumerable<string> additionalSources,
            CancellationToken token)
        {
            await Restore(jsonConfigPath, projectName, projectContext, additionalSources, token);
        }

        public static async Task<RestoreResult> Restore(string jsonConfigPath,
            string projectName,
            INuGetProjectContext projectContext,
            IEnumerable<string> additionalSources, 
            CancellationToken token)
        {
            // Limit to only 1 restore at a time
            await _semaphore.WaitAsync(token);

            try
            {
                token.ThrowIfCancellationRequested();

                return await RestoreCore(jsonConfigPath, projectName, projectContext, additionalSources, token);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private static async Task<RestoreResult> RestoreCore(string jsonConfigPath, string projectName, INuGetProjectContext projectContext, IEnumerable<string> sources, CancellationToken token)
        {
            FileInfo file = new FileInfo(jsonConfigPath);

            PackageSpec spec = JsonPackageSpecReader.GetPackageSpec(file.OpenRead(), projectName, jsonConfigPath);

            RestoreRequest request = new RestoreRequest(spec, sources.Select(source => new PackageSource(source)), file.Directory.FullName);

            var loggerFactory = new ProjectContextLoggerFactory(projectContext)
            {
                MinimumLevel = LogLevel.Debug
            };

            RestoreCommand command = new RestoreCommand(loggerFactory);

            return await command.ExecuteAsync(request);
        }

        private static async Task RestoreExternal(string jsonConfigPath, INuGetProjectContext projectContext, IEnumerable<string> sources, CancellationToken token)
        {
            FileInfo file = new FileInfo(jsonConfigPath);

            // Call DNU to restore
            string dnuPath = Environment.GetEnvironmentVariable("DNU_CMD_PATH");

            if (String.IsNullOrEmpty(dnuPath) || !dnuPath.EndsWith("dnu.cmd"))
            {
                throw new InvalidOperationException("Set the environment variable DNU_CMD_PATH to dnu.cmd");
            }
            else
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = dnuPath;
                startInfo.Arguments = "restore --ignore-failed-sources";
                startInfo.CreateNoWindow = true;
                startInfo.WorkingDirectory = file.Directory.FullName;
                startInfo.UseShellExecute = false;
                startInfo.RedirectStandardError = true;
                startInfo.RedirectStandardOutput = true;

                if (sources != null && sources.Any())
                {
                    foreach (var source in sources)
                    {
                        startInfo.Arguments += String.Format(CultureInfo.InvariantCulture, " -f {0}", source);
                    }
                }

                var process = new Process();
                process.StartInfo = startInfo;
                process.EnableRaisingEvents = true;

                process.ErrorDataReceived += (o, e) =>
                {
                    if (!String.IsNullOrEmpty(e.Data))
                    {
                        projectContext.Log(MessageLevel.Info, "{0}", e.Data);
                    }
                };

                process.OutputDataReceived += (o, e) =>
                {
                    projectContext.Log(MessageLevel.Info, "{0}", e.Data);
                };

                await Task.Run(() =>
                {
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        projectContext.ReportError(Strings.BuildIntegratedPackageRestoreFailed);
                    }
                });
            }
        }

        public static string GetNupkgPathFromGlobalSource(PackageIdentity identity)
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            string nupkgName = identity.Id + "." + identity.Version.ToNormalizedString() + ".nupkg";

            return Path.Combine(GlobalPackagesFolder, identity.Id, identity.Version.ToNormalizedString(), nupkgName);
        }

        public static string GlobalPackagesFolder
        {
            get
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                return Path.Combine(userProfile, ".dnx\\packages\\");
            }
        }
    }
}
