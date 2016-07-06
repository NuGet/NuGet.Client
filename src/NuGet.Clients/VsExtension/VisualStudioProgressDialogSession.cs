// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using NuGet.PackageManagement.UI;

namespace NuGetVSExtension
{
    internal class VisualStudioProgressDialogSession : IModalProgressDialogSession
    {
        private readonly Action _dispose;

        public VisualStudioProgressDialogSession(ThreadedWaitDialogHelper.Session session)
        {
            UserCancellationToken = session.UserCancellationToken;
            _dispose = session.Dispose;
            Progress = new VisualStudioDialogProgress(session.Progress);
        }

        public void Dispose()
        {
            _dispose();
        }

        public IProgress<ProgressDialogData> Progress { get; }
        public CancellationToken UserCancellationToken { get; }
    }
}