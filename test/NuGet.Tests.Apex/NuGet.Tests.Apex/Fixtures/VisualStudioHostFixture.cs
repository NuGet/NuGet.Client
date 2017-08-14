using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Test.Apex;
using Microsoft.Test.Apex.Interop;
using Microsoft.Test.Apex.Services;
using Microsoft.Test.Apex.VisualStudio;

namespace NuGet.Tests.Apex
{
    public class VisualStudioHostFixture : VisualStudioOperationsFixture, IDisposable
    {
        private VisualStudioHost _visualStudioHost;
        private RetryMessageFilter _messageFilterSingleton;

        public VisualStudioHost VisualStudio
        {
            get { return _visualStudioHost; }
        }

        public void EnsureHost()
        {
            if (_visualStudioHost == null || !_visualStudioHost.IsRunning)
            {
                _messageFilterSingleton = new RetryMessageFilter();
                _visualStudioHost = Operations.CreateAndStartHost<VisualStudioHost>(VisualStudioHostConfiguration);
            }
        }

        public void Dispose()
        {
            if (_visualStudioHost != null && _visualStudioHost.IsRunning)
            {
                var screenshotService = VisualStudio.Get<IScreenshotService>();
                try
                {
                    if (_messageFilterSingleton != null)
                    {
                        _messageFilterSingleton.Dispose();
                    }

                    _visualStudioHost.Stop();
                }
                catch (COMException)
                {
                    // VSO 178569: Access to DTE during shutdown may throw a variety of COM exceptions
                    // if inaccessible.
                }
                catch (Exception filterException)
                {
                    //this.Logger.WriteException(EntryType.Warning, filterException, "Could not to tear down the message filter.");
                }
                _visualStudioHost = null;
            }
        }
    }
}
