using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.PackageManagement.UI;
using NuGet.PackageManagement.VisualStudio;
using NuGetConsole;

namespace NuGetVSExtension
{
    internal class OutputConsoleLogger : INuGetUILogger
    {
        // Copied from EnvDTE interop
        private const string vsWindowKindOutput = "{34E76E81-EE4A-11D0-AE2E-00A0C90FFFC3}";

        public IConsole OutputConsole
        {
            get;
            private set;
        }

        public OutputConsoleLogger()
        {
            var outputConsoleProvider = ServiceLocator.GetInstance<IOutputConsoleProvider>();
            OutputConsole = outputConsoleProvider.CreateOutputConsole(requirePowerShellHost: false);
        }

        public void End()
        {
        }

        public void Log(NuGet.ProjectManagement.MessageLevel level, string message, params object[] args)
        {
            var s = string.Format(CultureInfo.CurrentCulture, message, args);
            OutputConsole.WriteLine(s);
        }

        private void ActivateOutputWindow()
        {
            var uiShell = ServiceLocator.GetGlobalService<SVsUIShell, IVsUIShell>();
            if (uiShell == null)
            {
                return;
            }

            var guid = new Guid(vsWindowKindOutput);
            IVsWindowFrame f = null;
            uiShell.FindToolWindow(0, ref guid, out f);
            if (f == null)
            {
                return;
            }

            f.Show();
        }

        public void Start()
        {
            ActivateOutputWindow();
            OutputConsole.Clear();
        }
    }
}