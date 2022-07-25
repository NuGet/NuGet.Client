// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Microsoft.Internal.VisualStudio.Shell.Embeddable.Feedback;
using Microsoft.VisualStudio.Shell;
using NuGet.Common;
using NuGet.PackageManagement;
using NuGet.VisualStudio.Telemetry;

#nullable enable

namespace NuGet.VisualStudio.Common
{
    [Export(typeof(IFeedbackDiagnosticFileProvider))]
    public class NuGetFeedbackDiagnosticFileProvider : IFeedbackDiagnosticFileProvider
    {
        // Don't crash VS feedback if imports fail. Maybe broken MEF is exactly what the customer is trying to report!
        [Import(AllowDefault = true)]
        public ISolutionManager? SolutionManager { get; set; }

        public IReadOnlyCollection<string> GetFiles()
        {
            string filePath = GetFilePath();

            // See comments on IFeedbackDiagnosticFileProvider.GetFiles
            ThreadHelper.JoinableTaskFactory.RunAsync(() => WriteToZip(filePath))
                .PostOnFailure(nameof(NuGetFeedbackDiagnosticFileProvider), nameof(WriteToZip));

            return new List<string>()
            {
                filePath
            };
        }

        public async Task WriteToZip(string filePath)
        {
            var telemetry = new TelemetryEvent("feedback");
            telemetry["IsDebuggerAttached"] = Debugger.IsAttached;
            bool successful = false;
            Stopwatch sw = Stopwatch.StartNew();
            try
            {

                using (var fileStream = File.Open(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var zip = new ZipArchive(fileStream, ZipArchiveMode.Create))
                {
                    await AddDgSpecAsync(zip);
                }

                successful = true;
            }
            finally
            {
                sw.Stop();
                telemetry["successful"] = successful;
                telemetry["duration_ms"] = sw.Elapsed.TotalMilliseconds;
                TelemetryActivity.EmitTelemetryEvent(telemetry);
            }
        }

        private string GetFilePath()
        {
            string fileNamePrefix = "nuget." + DateTime.UtcNow.ToString("yyyy-MM-dd.HH-mm-ss");
            int attempts = 0;
            for (; ; )
            {
                attempts++;
                var fileName = attempts == 0 ? fileNamePrefix + ".zip" : fileNamePrefix + "." + attempts + ".zip";
                var fullPath = Path.Combine(Path.GetTempPath(), fileName);
                if (!File.Exists(fullPath))
                {
                    return fullPath;
                }
            }
        }

        private async Task AddDgSpecAsync(ZipArchive zip)
        {
            string? tempFile = null;
            try
            {
                if (SolutionManager == null) { return; }

                var dgspec = await DependencyGraphRestoreUtility.GetSolutionRestoreSpec(SolutionManager, context: null);
                tempFile = Path.GetTempFileName();
                dgspec.Save(tempFile);

                _ = zip.CreateEntryFromFile(tempFile, "dgspec.json");
            }
            catch (Exception exception)
            {
                await TelemetryUtility.PostFaultAsync(exception, nameof(NuGetFeedbackDiagnosticFileProvider));
            }
            finally
            {
                if (tempFile != null)
                {
                    File.Delete(tempFile);
                }
            }
        }
    }
}
