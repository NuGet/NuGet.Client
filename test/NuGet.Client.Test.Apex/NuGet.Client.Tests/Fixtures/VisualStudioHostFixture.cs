using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Test.Apex.VisualStudio;
using Microsoft.Test.Apex.Services;
using NuGetClient.Test.Foundation.Utility;

namespace NuGetClient.Test.Integration.Fixtures
{
    /// <summary>
    /// This fixture provides access to an instance of Visual Studio via
    /// Apex, and manages the lifetime of that process
    /// </summary>
    public class VisualStudioHostFixture : VisualStudioOperationsFixture, IDisposable
    {
        private VisualStudioHost visualStudioHost;
        public VisualStudioHost VisualStudio
        {
            get { return this.visualStudioHost; }
        }
        public VisualStudioHost Host
        {
            get { return visualStudioHost; }
        }

        public void SetHostEnvironment(string name, string value)
        {
            if (value == null)
            {
                base.VisualStudioHostConfiguration.Environment.Remove(name);
            }
            else
            {
                base.VisualStudioHostConfiguration.Environment[name] = value;
            }
        }

        public string GetHostEnvironment(string name)
        {
            if (!base.VisualStudioHostConfiguration.Environment.ContainsKey(name))
            {
                return null;
            }

            return base.VisualStudioHostConfiguration.Environment[name];
        }

        public void EnsureHost()
        {
            if (visualStudioHost == null || !visualStudioHost.IsRunning)
            {
                this.visualStudioHost = this.FixtureOperations.CreateAndStartHost<VisualStudioHost>(VisualStudioHostConfiguration);
                var compose = VisualStudioHostConfiguration.CompositionAssemblies;
            }
        }
        public void Dispose()
        {
            TestUIThreadHelper.Instance.InvokeOnTestUIThread(() =>
            {
                if (this.visualStudioHost != null && this.visualStudioHost.IsRunning)
                {
                    IScreenshotService screenshotService = this.VisualStudio.Get<IScreenshotService>();
                    try
                    {
                        this.visualStudioHost.Stop();
                    }
                    catch (COMException)
                    {
                        // VSO 178569: Access to DTE during shutdown may throw a variety of COM exceptions
                        // if inaccessible. First, check if the process has gone out from under us.
                        try
                        {
                            Process process = Process.GetProcessById(this.visualStudioHost.HostProcess.Id);
                            // We still have a process, If this happens, take a screenshot and a dump, then throw.
                            if (process != null)
                            {
                                if (screenshotService != null)
                                {
                                    screenshotService.TakeOne("NuGetClientFixtureTeardown", "Dispose_COMException" + Guid.NewGuid().ToString("D"));
                                }
                                this.visualStudioHost.CaptureHostProcessDumpIfRunning();
                                throw;
                            }
                        }
                        catch (ArgumentException)
                        {
                            // Process is not running
                        }
                        catch (InvalidOperationException)
                        {
                            // Process was not started.
                        }
                    }
                    this.visualStudioHost = null;
                }
            });
        }
    }
}
