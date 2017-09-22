using System;
using System.Runtime.InteropServices;
using Microsoft.Test.Apex.Interop;
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
                var compose = VisualStudioHostConfiguration.CompositionAssemblies;
            }
        }

        public void Dispose()
        {
            if (_visualStudioHost != null && _visualStudioHost.IsRunning)
            {
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
