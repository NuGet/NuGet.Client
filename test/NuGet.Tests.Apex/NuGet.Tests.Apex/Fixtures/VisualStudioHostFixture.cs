// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

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

        public bool IsHostRunning => _visualStudioHost != null && _visualStudioHost.IsRunning;

        public void EnsureHost()
        {
            if (!IsHostRunning)
            {
                _messageFilterSingleton = new RetryMessageFilter();
                _visualStudioHost = Operations.CreateAndStartHost<VisualStudioHost>(VisualStudioHostConfiguration);
                var compose = VisualStudioHostConfiguration.CompositionAssemblies;
            }
        }

        public void Dispose()
        {
            try
            {
                if (_messageFilterSingleton != null)
                {
                    _messageFilterSingleton.Dispose();
                }

            }
            catch (COMException)
            {
                // VSO 178569: Access to DTE during shutdown may throw a variety of COM exceptions
                // if inaccessible.
            }
            catch (Exception)
            {
                //this.Logger.WriteException(EntryType.Warning, filterException, "Could not to tear down the message filter.");
            }

            try
            {
                if (IsHostRunning)
                {
                    _visualStudioHost.Stop();
                }
            }
            catch (COMException)
            {
                // VSO 178569: Access to DTE during shutdown may throw a variety of COM exceptions
                // if inaccessible.
            }
            catch (Exception)
            {
                //this.Logger.WriteException(EntryType.Warning, filterException, "Could not stop the Visual Studio host.");
            }

            _messageFilterSingleton = null;
            _visualStudioHost = null;
        }
    }
}
