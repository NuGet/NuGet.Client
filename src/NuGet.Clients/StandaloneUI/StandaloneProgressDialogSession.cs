// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using NuGet.PackageManagement.UI;
using NuGet.ProjectManagement;

namespace StandaloneUI
{
    internal class StandaloneProgressDialogSession : IModalProgressDialogSession
    {
        private readonly INuGetProjectContext _logger;
        private readonly string _caption;

        public StandaloneProgressDialogSession(string caption, ProgressDialogData initialData, INuGetUI uiService)
        {
            // For the standalone implementation, just write to the output console.

            _caption = caption;
            _logger = uiService.ProgressWindow;
            UserCancellationToken = CancellationToken.None;
            Progress = new StandaloneDialogProgress(caption, _logger);

            _logger.Log(MessageLevel.Info, $"Progress dialog '{caption}' opening.");
            _logger.Log(MessageLevel.Info, $"Progress dialog '{caption}': {initialData.ProgressText}: {initialData.WaitMessage}");
        }

        public void Dispose()
        {
            _logger.Log(MessageLevel.Info, $"Progress dialog '{_caption}': closing.");
        }

        public IProgress<ProgressDialogData> Progress { get; }
        public CancellationToken UserCancellationToken { get; }
    }
}