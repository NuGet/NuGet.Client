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

        [Import(AllowDefault = true)]
        public INuGetTelemetryProvider? TelemetryProvider { get; set; }

        /// <summary>Used for testing, so tests can wait for the background task to finish.</summary>
        public event EventHandler<Task>? BackgroundTaskStarted;

        public IReadOnlyCollection<string> GetFiles()
        {
            FileStream fileStream = GetOutputFile();

            // See comments on IFeedbackDiagnosticFileProvider.GetFiles
            var task = Task.Run(() => WriteToZipAndCloseFileAsync(fileStream));
            BackgroundTaskStarted?.Invoke(this, task);

            return new List<string>()
            {
                fileStream.Name
            };
        }

        private async Task WriteToZipAndCloseFileAsync(Stream stream)
        {
            using (stream)
            {
                try
                {
                    await WriteToZipAsync(stream);
                }
                catch (Exception exception)
                {
                    await TelemetryUtility.PostFaultAsync(exception, nameof(NuGetFeedbackDiagnosticFileProvider), nameof(WriteToZipAndCloseFileAsync));
                }
            }
        }

        private FileStream GetOutputFile()
        {
            string fileNamePrefix = "nuget." + DateTime.UtcNow.ToString("yyyy-MM-dd.HH-mm-ss");
            for (int attempt = 0; attempt < 100; attempt++)
            {
                var fileName = attempt == 0
                    ? fileNamePrefix + ".zip"
                    : fileNamePrefix + "." + attempt + ".zip";
                var fullPath = Path.Combine(Path.GetTempPath(), fileName);
                try
                {
                    FileStream fileStream = new FileStream(fullPath, FileMode.CreateNew);
                    return fileStream;
                }
                catch (IOException)
                {
                    // Unlikely that the customer is going to click "report a problem" more than once per second, but just in case
                }
            }

            throw new Exception("Unable to create file to attach");
        }

        public async Task WriteToZipAsync(Stream stream)
        {
            var telemetry = new TelemetryEvent("feedback");
            telemetry["IsDebuggerAttached"] = Debugger.IsAttached;
            bool successful = false;
            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
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
                EmitTelemetryEvent(telemetry);
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

        private void EmitTelemetryEvent(TelemetryEvent telemetryEvent)
        {
            if (TelemetryProvider != null)
            {
                TelemetryProvider.EmitEvent(telemetryEvent);
            }
            else
            {
                TelemetryActivity.EmitTelemetryEvent(telemetryEvent);
            }
        }
    }
}
