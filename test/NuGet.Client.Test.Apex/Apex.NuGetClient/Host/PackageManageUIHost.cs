using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Apex.NuGetClient.ObjectModel;
using Microsoft.Test.Apex;
using Microsoft.Test.Apex.Hosts;
using Microsoft.Test.Apex.Hosts.Services;

namespace Apex.NuGetClient.Host
{
    [DefaultHostConfiguration(typeof(AppContainerUIHostConfiguration))]
    public abstract class AppContainerUIHost : ExternalUIHost
    {
    }
    public class AppContainerUIHostConfiguration : ExternalUIHostConfiguration
    {
        public AppContainerUIHostConfiguration()
        {
            this.SupportsAssemblyLoadFrom = false;
        }
    }

    /// <summary>
    /// Test target host
    /// </summary>
    [DefaultHostConfiguration(typeof(PackageManageUIHostConfiguration))]
    public class PackageManageUIHost : AppContainerUIHost
    {
        /// <summary>
        /// Object Model for the Package Manage UI windows
        /// </summary>
        private PackageManageUIObjectModel objectModel = null;

        public override IntPtr MainWindowHandle
        {
            get
            {
                if (this.mainWindowHandle == IntPtr.Zero)
                {
                    Debug.Assert(this.Configuration.PackageManageUIHostProcessId != 0);

                    // Wait for 3 seconds for the package manager to load
                    for (int x = 0; x < 6; x++)
                    {
                        NativeMethods.EnumWindowsProc callback = new NativeMethods.EnumWindowsProc(EnumerateTopLevelWindows);
                        NativeMethods.EnumWindows(callback, IntPtr.Zero);
                        GC.KeepAlive(callback);

                        if (this.mainWindowHandle != IntPtr.Zero)
                        {
                            break;
                        }

                        System.Threading.Thread.Sleep(500);
                    }

                    if (this.mainWindowHandle == IntPtr.Zero)
                    {
                        Logger.WriteError("Failed to find child HWND for designer process.");
                        this.CollectDumpsFromHostAndDesignerProcess();
                    }

                }

                return this.mainWindowHandle;
            }
        }
        public void CollectDumpsFromHostAndDesignerProcess(MiniDumpType dumpType = MiniDumpType.WithFullMemory)
        {
            if (!string.IsNullOrWhiteSpace(this.Configuration.LocalDumpStorageDirectory))
            {
                this.CollectDump(this.Configuration.PackageManageUIHostProcessId, dumpType);
                this.CollectDump(this.HostProcess.Id, dumpType);
            }
        }

        public override ExternalProcessHostStartInfo ProcessStartInfo => throw new NotImplementedException();

        /// <summary>
        /// the main window handle
        /// </summary>
        private IntPtr mainWindowHandle = IntPtr.Zero;

        [Import(AllowDefault = true)]
        private Lazy<IDumpCollectorService> lazyDumpCollector
        {
            get;
            set;
        }

        public PackageManageUIHost()
        {
            this.ExportsToHostTypeConstraints = new List<ITypeConstraint>
            {
                new PackageManageUIExportToHostTypeConstraint(this.GetType())
            };
        }

        /// <summary>
        /// End Process
        /// </summary>
        protected override void EndProcess()
        {
            this.HostProcess.Kill();
        }

        /// <summary>
        /// No process attached, return false
        /// </summary>
        /// <param name="process"></param>
        /// <returns></returns>
        protected override bool IsProcessAttachable(Process process)
        {
            return false;
        }

        public PackageManageUIObjectModel ObjectModel
        {
            get
            {
                if (this.objectModel == null)
                {
                    try
                    {
                        this.objectModel = this.ObjectModelFactory.Create(this);
                    }
                    catch (Exception ex)
                    {
                        string message = String.Format(
                            CultureInfo.InvariantCulture,
                            "No factory product providers for '{0}' could create an instance of '{1}', you may be running '{2}' in a context where the Object Model is not available or implemented. See inner exception for details",
                            typeof(PackageManageUIObjectModelFactoryService),
                            typeof(PackageManageUIObjectModel),
                            this.GetType());
                    }
                }

                return this.objectModel;
            }
        }

        private bool EnumerateTopLevelWindows(IntPtr hwnd, ArrayList lParam)
        {
            // enumerate all top level windows owned by the host process
            uint pid;
            NativeMethods.GetWindowThreadProcessId(hwnd, out pid);

            if (this.Configuration.PackageManageUIHostProcessId == pid)
            {
                // for each top level window in VS, enumerate all the child windows to find the correct host.
                NativeMethods.EnumWindowsProc callback = new NativeMethods.EnumWindowsProc(EnumerateChildWindows);
                NativeMethods.EnumChildWindows(hwnd, callback, IntPtr.Zero);
                GC.KeepAlive(callback);

                // if it has not been set by the children of this top level hwnd, return true to continue enumerating.
                return (this.mainWindowHandle == IntPtr.Zero);
            }

            return true; // continue enumeration
        }

        private bool EnumerateChildWindows(IntPtr hwnd, ArrayList lParam)
        {
            // we're specifically looking for an HWND
            uint pid;
            NativeMethods.GetWindowThreadProcessId(hwnd, out pid);

            StringBuilder stringBuilder = new StringBuilder(256);
            NativeMethods.GetWindowText(hwnd, stringBuilder, stringBuilder.Capacity);

            //check to see if it is an appropriate RemoteContentHolder window.
            if (stringBuilder.ToString().Equals("RemoteContentHolder", StringComparison.OrdinalIgnoreCase))
            {
                this.mainWindowHandle = hwnd;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Gets the configuration class for this host
        /// </summary>
        public new PackageManageUIHostConfiguration Configuration
        {
            get
            {
                return base.Configuration as PackageManageUIHostConfiguration;
            }
        }
        /// <summary>
        /// Gets or sets the object model factory.
        /// </summary>
        /// <value>The object model factory.</value>
        [Import]
        private PackageManageUIObjectModelFactoryService ObjectModelFactory
        {
            get;
            set;
        }

        protected IDumpCollectorService DumpCollector
        {
            get { return this.lazyDumpCollector.Value; }
        }

        /// <summary>
        /// Collects a memory dump of the specified process.
        /// </summary>
        /// <param name="processId">The process to collect a dump from.</param>
        private void CollectDump(int processId, MiniDumpType dumpType = MiniDumpType.WithFullMemory)
        {
            Logger.WriteMessage("Attempting to collect a memory dump from process ID '{0}'", processId);
            if (this.DumpCollector == null)
            {
                Logger.WriteWarning("Could not collect memory dump as no IDumpCollector was available.");
                return;
            }

            Process process = Process.GetProcessById(processId);
            if (process == null)
            {
                Logger.WriteWarning("Could not find a process with ID '{0}'", processId);
                return;
            }

            string localDumpRoot = this.Configuration.LocalDumpStorageDirectory;
            if (!Directory.Exists(localDumpRoot))
            {
                Directory.CreateDirectory(localDumpRoot);
            }

            string hostDumpFileName = string.Format("{0}_{1}_{2}.dmp", process.ProcessName, process.Id, DateTime.Now.ToString("yyyy-MM-dd-THHmmss"));
            string hostDumpFile = Path.Combine(localDumpRoot, hostDumpFileName);
            if (File.Exists(hostDumpFile))
            {
                Logger.WriteWarning("File '{0}' already exists, aborting dump", hostDumpFile);
                return;
            }

            FileInfo fileInfo = new FileInfo(hostDumpFile);
            this.DumpCollector.WriteDump(process, fileInfo, dumpType);
        }
    }
}
